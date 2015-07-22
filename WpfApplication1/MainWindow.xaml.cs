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

namespace WpfApplication1
{
    public partial class MainWindow : Window
    {
        private const int REDRAW_INTERVAL_MS = 30;

        private DispatcherTimer _graphRedrawTimer;
        private IByteArrDataParser _parser;
        private SerialPort _serialPort;
        private byte[] _serialPortBuffer = new byte[16384];
        private MainViewModel _mainViewModel { get { return DataContext as MainViewModel; } }
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

                    this.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        _mainViewModel.AddZ(_dataPointCounter, v);
                        if (_calibrated) _mainViewModel.AddSpeed(_dataPointCounter, speed * 10);
                    }));

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
            _graphRedrawTimer.Tick += (sender, e) => { _mainViewModel.Invalidate(); };
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
            _mainViewModel.MyModel.ResetAllAxes();
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
    }

    public class MainViewModel
    {
        private const double COMMAND_RATE = 99.058823529411764705882352941176; // RATE ON DEVICE = 72
        private const int SECONDS_TO_REMEMBER = 600;
        private const int LOWPASS_FREQ = 5;

        public PlotModel MyModel { get; private set; }

        private LineSeries _z = new LineSeries("Accel Z");
        private LineSeries _speedZ = new LineSeries("Speed Z");

        public MainViewModel()
        {
            this.MyModel = new PlotModel { Title = "Accel Data" };
            this.MyModel.Series.Add(_z);
            this.MyModel.Series.Add(_speedZ);
        }

        public void Invalidate() { MyModel.InvalidatePlot(true); }

        public void AddZ(double x, double y)
        {
            _z.Points.Add(new DataPoint(x, y));
            while (_z.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _z.Points.RemoveAt(0);
            }
        }
        public void AddSpeed(double x, double y)
        {
            _speedZ.Points.Add(new DataPoint(x, y));
            while (_speedZ.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _speedZ.Points.RemoveAt(0);
            }
        }

        public double GetZMin()
        {
            if (_z.Points.Count == 0) { return -1; }
            else { return _z.Points.Min(m => m.Y); }
        }

        public double GetZMax()
        {
            if (_z.Points.Count == 0) { return -1; }
            else { return _z.Points.Max(m => m.Y); }
        }

        public double GetZMedian()
        {
            if (_z.Points.Count == 0) { return -1; }
            else
            {
                return _z.Points.Average(a => a.Y);
                //int count = _z.Points.Count();
                //var orderedPoints = _z.Points.OrderBy(p => p.Y);
                //double median = _z.Points.ElementAt(count / 2).Y + orderedPoints.ElementAt((count - 1) / 2).Y;
                //median /= 2;
                //return median;
            }
        }

        public void Clear()
        {
            _z.Points.Clear();
            _speedZ.Points.Clear();

        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(new List<DataPoint>[2] { _z.Points, _speedZ.Points });
        }

        public int Deserialize(string serialized)
        {
            try
            {
                List<DataPoint>[] result = JsonConvert.DeserializeObject<List<DataPoint>[]>(serialized);
                Clear();
                _z.Points.AddRange(result[0]);
                _speedZ.Points.AddRange(result[1]);

                return (int)_z.Points.Max(p => p.X);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
