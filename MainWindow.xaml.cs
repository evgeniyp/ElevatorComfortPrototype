using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OxyPlot.Series;
using OxyPlot;
using System.Windows.Threading;
using System.IO.Ports;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.IO;
using Filters;
using System.Diagnostics;

namespace ElevatorComfort
{
    public partial class MainWindow : Window
    {
        private Model _model = new Model();

        private const int REDRAW_INTERVAL_MS = 10;

        private DispatcherTimer _graphRedrawTimer;
        private IByteArrDataParser _parser;
        private SerialPort _serialPort;
        private byte[] _serialPortBuffer = new byte[16384];
        private ViewModel _mainViewModel { get { return DataContext as ViewModel; } }
        private Stopwatch _stopwatch = new Stopwatch();
        private int _dataPointCounter;
        private double _lastX;
        private double _lastY;
        private long _lastPointTime;
        private TwoPoleButterworthFilter _filterZ = new TwoPoleButterworthFilter();
        //private FilterButterworth2 _filterZ = new FilterButterworth2(1);
        private AccelToSpeed _accelToSpeed = new AccelToSpeed();
        private bool _calibrated = false;

        public MainWindow()
        {
            InitializeComponent();

            _stopwatch.Start();
            InitializeComboBoxComPorts();
            InitializeParser();
            InitializeRedrawTimer();
        }

        private void InitializeComboBoxComPorts()
        {
            ComboBoxComPorts.Items.Clear();
            var portNames = SerialPort.GetPortNames();
            foreach (var portName in portNames)
            {
                ComboBoxComPorts.Items.Add(portName);
            }
            if (ComboBoxComPorts.Items.Count > 0)
            {
                ComboBoxComPorts.SelectedIndex = 0;
            }
        }

        private void InitializeAccelToSpeed(double min, double max)
        {
            _accelToSpeed.Reset();
        }

        private void InitializeParser()
        {
            _parser = new UM6LTSensorParser();
        }

        private void _parser_DataParsed(string name, object value)
        {
            var thisPointTime = _stopwatch.ElapsedMilliseconds;
            var deltaSeconds = (thisPointTime - _lastPointTime) / 1000.0;

            switch (name)
            {
                case "X":
                    _lastX = (float)value;
                    break;
                case "Y":
                    _lastY = (float)value;
                    break;
                case "Z":
                    double v = Math.Sqrt((float)value * (float)value + _lastX * _lastX + _lastY * _lastY);
                    v = _filterZ.Next(v);

                    double speed = 0;
                    if (_calibrated)
                    {
                        speed = _accelToSpeed.Next(v, deltaSeconds);
                    }

                    _model.AddAccel(_dataPointCounter, v);

                    _dataPointCounter++;
                    _lastPointTime = thisPointTime;

                    break;
                default:
                    break;
            }
        }

        private void InitializeRedrawTimer()
        {
            _graphRedrawTimer = new DispatcherTimer();
            _graphRedrawTimer.Tick += (sender, e) => { _mainViewModel.SetSeries(_model.GetAccel()); };
            _graphRedrawTimer.Interval = TimeSpan.FromMilliseconds(REDRAW_INTERVAL_MS);
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
            _serialPort.DataReceived += _serialPort_DataReceived;

            if (!_serialPort.IsOpen) { _serialPort.Open(); }
        }

        private void CloseSerialPort()
        {
            if (_serialPort != null)
            {
                _serialPort.DataReceived -= _serialPort_DataReceived;
                if (_serialPort.IsOpen) { _serialPort.Close(); }
            }
        }

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                var bytesToRead = _serialPort.BytesToRead;
                sp.Read(_serialPortBuffer, 0, bytesToRead);
                _parser.HandleData(_serialPortBuffer, bytesToRead);
            }
            catch (Exception e) { this.Dispatcher.BeginInvoke((Action)(() => { this.Title = e.Message; })); }
        }

        private void EnableDataEntry(bool enable, string comPortName = "")
        {
            try
            {
                if (enable)
                {
                    _parser.DataParsed += _parser_DataParsed;
                    OpenSerialPort(comPortName);
                    _graphRedrawTimer.IsEnabled = true;
                }
                else
                {
                    _parser.DataParsed -= _parser_DataParsed;
                    CloseSerialPort();
                    _graphRedrawTimer.IsEnabled = false;
                }
            }
            catch (System.IO.IOException e) { this.Title = e.Message; }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Content.ToString() == "Start")
            {
                button.Content = "Stop";
                if (ComboBoxComPorts.SelectedItem == null) { return; }
                ComboBoxComPorts.IsEnabled = false;
                ButtonLoad.IsEnabled = false;
                ButtonSave.IsEnabled = false;
                EnableDataEntry(true, ComboBoxComPorts.SelectedItem.ToString());
            }
            else
            {
                button.Content = "Start";
                ComboBoxComPorts.IsEnabled = true;
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
            openFileDialog.Filter = "XYZ axis files |*.xyz";
            openFileDialog.Multiselect = false;
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var stringToDeserialize = File.ReadAllText(openFileDialog.FileName);
                    _dataPointCounter = _mainViewModel.Deserialize(stringToDeserialize);

                    Calibrate();

                    _mainViewModel.Invalidate();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(exception.Message, "Load Error");
                    Reset();
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XYZ axis files |*.xyz";
            if (saveFileDialog.ShowDialog() == true)
            {
                try { File.WriteAllText(saveFileDialog.FileName, _mainViewModel.Serialize()); }
                catch (Exception exception) { MessageBox.Show(exception.Message, "Save Error"); }
            }
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void Reset()
        {
            _mainViewModel.Clear();
            _dataPointCounter = 0;
            _accelToSpeed.Reset();
            _calibrated = false;
            _mainViewModel.Invalidate();
        }

        private string Calibrate()
        {
            double min = _mainViewModel.GetZMin(),
                   max = _mainViewModel.GetZMax(),
                   median = _mainViewModel.GetZMedian();

            _accelToSpeed.SetToDeadZone(min, max, median);
            _accelToSpeed.Reset();

            _calibrated = true;

            return String.Format("{0:0.000000} - {1:0.000000}", min, max);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EnableDataEntry(false);
        }

        private void CalibrateSpeedFromAcc_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            button.Content = Calibrate();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _mainViewModel.Plot.Series[0].IsVisible = false;
        }
    }
}
