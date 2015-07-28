using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO.Ports;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using Parsers;
using NAudio.Wave;

namespace ElevatorComfort
{
    public partial class MainWindow : Window
    {
        public class ComboBoxItem
        {
            public string Text { get; set; }
            public int Value { get; set; }

            public override string ToString() { return Text; }
        }

        private const string FILE_DIALOG_FILTER = "Data sample files |*.ds";

        private const double DEVICE_FPS = 184.7058823529412; // device rate register = 150 (280 / 255 * DEVICE_FPS + 20)
        private const int SECONDS_TO_REMEMBER = 60;
        private Model _model = new Model((int)(DEVICE_FPS * SECONDS_TO_REMEMBER));

        private ViewModel _mainViewModel { get { return DataContext as ViewModel; } }

        private const int REDRAW_INTERVAL_MS = 20;
        private DispatcherTimer _redrawTimer = new DispatcherTimer();

        private IByteArrDataParser _parser = new UM6LTSensorParser();

        private SerialPort _serialPort;
        private byte[] _serialPortBuffer = new byte[256];
        private Thread _serialPortReader;

        private const int SAMPLE_RATE = 44100;
        private const int MAX_SAMPLE_COUNTER = (int)(SAMPLE_RATE / DEVICE_FPS);
        private WaveIn _waveIn;
        private short _maxSample;
        private int _sampleCounter;

        //private Stopwatch _stopwatch = new Stopwatch();
        private double _manualStopwatchCounter = 0;
        private const double FRAME_LENGTH = 1 / DEVICE_FPS;

        private double _lastX;
        private double _lastY;

        public MainWindow()
        {
            InitializeComponent();

            InitializeComboBoxComPorts();
            InitializeComboBoxAudioIns();
            InitializeRedrawTimer();
        }

        private void InitializeComboBoxComPorts()
        {
            ComboBoxComPorts.Items.Clear();
            var portNames = SerialPort.GetPortNames();
            foreach (var portName in portNames) { ComboBoxComPorts.Items.Add(portName); }
            if (ComboBoxComPorts.Items.Count > 0) { ComboBoxComPorts.SelectedIndex = 0; }
        }

        private void InitializeComboBoxAudioIns()
        {
            ComboBoxAudioIns.Items.Clear();
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                ComboBoxAudioIns.Items.Add(new ComboBoxItem() { Text = deviceInfo.ProductName, Value = waveInDevice });
            }
            if (ComboBoxAudioIns.Items.Count > 0) { ComboBoxAudioIns.SelectedIndex = 0; }
        }

        private void InitializeSelectedAudioIn(int waveInDevice)
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= _waveIn_DataAvailable;
                _waveIn.Dispose();
            }

            _waveIn = new WaveIn();
            _waveIn.DeviceNumber = waveInDevice;
            _waveIn.WaveFormat = new WaveFormat(44100, 1);
            _waveIn.DataAvailable += _waveIn_DataAvailable;
            _waveIn.BufferMilliseconds = (int)(1000 / DEVICE_FPS);
            _waveIn.StartRecording();
        }

        private void _waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            byte[] buffer = e.Buffer;
            int bytesRecorded = e.BytesRecorded;

            for (int index = 0; index < e.BytesRecorded; index += 2)
            {
                short sample = (short)((buffer[index + 1] << 8) | buffer[index + 0]);

                _maxSample = Math.Max(sample, _maxSample);
                _sampleCounter++;
                if (_sampleCounter > MAX_SAMPLE_COUNTER)
                {
                    //Dispatcher.BeginInvoke((Action)(() => { }));
                    ProgressBarAudioLevel.Value = _maxSample;
                    _maxSample = 0;
                    _sampleCounter = 0;
                }
            }
        }

        private void ComboBoxAudioIns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeSelectedAudioIn(((sender as ComboBox).SelectedValue as ComboBoxItem).Value);
        }

        private void InitializeRedrawTimer()
        {
            _redrawTimer.Tick += (sender, e) =>
            {
                _mainViewModel.SetSeries(_model.GetPoints());
                _mainViewModel.Invalidate();
            };
            _redrawTimer.Interval = TimeSpan.FromMilliseconds(REDRAW_INTERVAL_MS);
        }

        private void OpenSerialPort(string portName)
        {
            _serialPort = new SerialPort(portName);
            _serialPort.Encoding = System.Text.Encoding.ASCII;
            _serialPort.BaudRate = 115200;
            _serialPort.Parity = Parity.None;
            _serialPort.StopBits = StopBits.One;
            _serialPort.DataBits = 8;
            _serialPort.Handshake = Handshake.None;
            //_serialPort.DataReceived += _serialPort_DataReceived;

            if (!_serialPort.IsOpen) { _serialPort.Open(); }

            _serialPortReader = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        var __btr = _serialPort.BytesToRead;
                        if (__btr > 0)
                        {
                            int __bytes_read = _serialPort.Read(_serialPortBuffer, 0, Math.Min(_serialPortBuffer.Length, __btr));
                            _parser.HandleData(_serialPortBuffer, __bytes_read);
                        }
                        else Thread.Sleep(1);
                    }
                }
                catch (ThreadAbortException) { }
                catch (Exception e) { Dispatcher.BeginInvoke((Action)(() => { Title = e.Message; })); }
            });
            _serialPortReader.Start();

        }

        private void CloseSerialPort()
        {
            if (_serialPort != null)
            {
                _serialPortReader.Abort();

                if (_serialPort.IsOpen) { _serialPort.Close(); }
                //_serialPort.DataReceived -= _serialPort_DataReceived;
            }
        }

        //private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
        //{
        //    try
        //    {
        //        SerialPort sp = (SerialPort)sender;
        //        var bytesToRead = _serialPort.BytesToRead;
        //        sp.Read(_serialPortBuffer, 0, bytesToRead);
        //        _parser.HandleData(_serialPortBuffer, bytesToRead);
        //    }
        //    catch (Exception e) { Dispatcher.BeginInvoke((Action)(() => { Title = e.Message; })); }
        //}

        private void _parser_DataParsed(string name, object value)
        {
            switch (name)
            {
                case "X":
                    _lastX = (float)value;
                    break;
                case "Y":
                    _lastY = (float)value;
                    break;
                case "Z":
                    _model.AddValues(_lastX, _lastY, (float)value, _maxSample / (double)short.MaxValue, _manualStopwatchCounter, FRAME_LENGTH);
                    _manualStopwatchCounter += FRAME_LENGTH;
                    break;
                default:
                    break;
            }
        }

        private void EnableDataEntry(bool enable, string comPortName = "")
        {
            try
            {
                if (enable)
                {
                    _parser.DataParsed += _parser_DataParsed;
                    OpenSerialPort(comPortName);
                    _redrawTimer.IsEnabled = true;
                }
                else
                {
                    _parser.DataParsed -= _parser_DataParsed;
                    CloseSerialPort();
                    _redrawTimer.IsEnabled = false;
                }
            }
            catch (System.IO.IOException e) { Title = e.Message; }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Content.ToString() == "Start")
            {
                if (ComboBoxComPorts.SelectedItem == null) { return; }

                _model.Reset();

                button.Content = "Stop";
                ComboBoxComPorts.IsEnabled = false;
                ComboBoxAudioIns.IsEnabled = false;
                ButtonLoad.IsEnabled = false;
                ButtonSave.IsEnabled = false;

                _manualStopwatchCounter = 0;
                //_stopwatch.Restart();

                EnableDataEntry(true, ComboBoxComPorts.SelectedItem.ToString());
            }
            else
            {
                button.Content = "Start";
                ComboBoxComPorts.IsEnabled = true;
                ComboBoxAudioIns.IsEnabled = true;
                ButtonLoad.IsEnabled = true;
                ButtonSave.IsEnabled = true;
                EnableDataEntry(false);
            }
        }

        private void ButtonFitAll_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.Plot.ResetAllAxes();
            _mainViewModel.Invalidate();
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = FILE_DIALOG_FILTER;
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _model.Deserialize(File.ReadAllText(openFileDialog.FileName));
                    _mainViewModel.SetSeries(_model.GetPoints());
                    _mainViewModel.Invalidate();
                }
                catch (Exception exception) { MessageBox.Show(exception.Message, "Load Error"); }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = FILE_DIALOG_FILTER;
            if (saveFileDialog.ShowDialog() == true)
            {
                try { File.WriteAllText(saveFileDialog.FileName, _model.Serialize()); }
                catch (Exception exception) { MessageBox.Show(exception.Message, "Save Error"); }
            }
        }

        private void CalibrateSpeedFromAcc_Click(object sender, RoutedEventArgs e)
        {
            _model.Calibrate();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                ButtonStart_Click(ButtonStart, null);
                return;
            }

            int seriesIndex;
            switch (e.Key)
            {
                case System.Windows.Input.Key.D1: seriesIndex = 0; break;
                case System.Windows.Input.Key.D2: seriesIndex = 1; break;
                case System.Windows.Input.Key.D3: seriesIndex = 2; break;
                case System.Windows.Input.Key.D4: seriesIndex = 3; break;
                case System.Windows.Input.Key.D5: seriesIndex = 4; break;
                case System.Windows.Input.Key.D6: seriesIndex = 5; break;
                case System.Windows.Input.Key.D7: seriesIndex = 6; break;
                case System.Windows.Input.Key.D8: seriesIndex = 7; break;
                default: seriesIndex = -1; break;
            }

            if (seriesIndex == -1) { return; }

            _mainViewModel.ToggleVisibleSeries(seriesIndex);
            _mainViewModel.Invalidate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EnableDataEntry(false);
        }
    }
}
