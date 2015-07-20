using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OxyPlot.Series;
using OxyPlot;
using System.Windows.Threading;
using System.IO.Ports;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.IO;
using Filters;

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
        private int _dataPointCounter;

        public MainWindow()
        {
            InitializeComponent();

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

        private void InitializeParser()
        {
            _parser = new UM6LTSensorParser();
        }

        private void _parser_DataParsed(string name, object value)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                switch (name)
                {
                    case "X": _mainViewModel.AddX(_dataPointCounter, (float)value); break;
                    case "Y": _mainViewModel.AddY(_dataPointCounter, (float)value); break;
                    case "Z": _mainViewModel.AddZ(_dataPointCounter, (float)value); _dataPointCounter++; break;
                    default:
                        break;
                }
            }));
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
            _serialPort.Open();
        }

        private void CloseSerialPort()
        {
            _serialPort.DataReceived -= _serialPort_DataReceived;
            _serialPort.Close();
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
                    _dataPointCounter = _mainViewModel.DeserializeXYZ(stringToDeserialize);
                    _mainViewModel.Invalidate();
                }
                catch (Exception exception) 
                {
                    MessageBox.Show(exception.Message, "Load Error");
                    ClearAll();
                }
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XYZ axis files |*.xyz";
            if (saveFileDialog.ShowDialog() == true)
            {
                try { File.WriteAllText(saveFileDialog.FileName, _mainViewModel.SerializeXYZ()); }
                catch (Exception exception) { MessageBox.Show(exception.Message, "Save Error"); }
            }
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
        }

        private void ClearAll()
        {
            _mainViewModel.ClearXYZ();
            _dataPointCounter = 0;
            _mainViewModel.Invalidate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EnableDataEntry(false);
        }
    }

    public class MainViewModel
    {
        private const double COMMAND_RATE = 99.058823529411764705882352941176; //20;
        private const int SECONDS_TO_REMEMBER = 60;
        private const int LOWPASS_FREQ = 5;

        private TwoPoleButterworthFilter _filterX = new TwoPoleButterworthFilter();
        private TwoPoleButterworthFilter _filterY = new TwoPoleButterworthFilter();
        private TwoPoleButterworthFilter _filterZ = new TwoPoleButterworthFilter();

        public PlotModel MyModel { get; private set; }

        private LineSeries _x = new LineSeries("X");
        private LineSeries _y = new LineSeries("Y");
        private LineSeries _z = new LineSeries("Z");

        public MainViewModel()
        {
            this.MyModel = new PlotModel { Title = "Accel Data" };
            this.MyModel.Series.Add(_x);
            this.MyModel.Series.Add(_y);
            this.MyModel.Series.Add(_z);
        }

        public void Invalidate() { MyModel.InvalidatePlot(true); }

        public void AddX(double x, double y)
        {
            y = _filterX.Next(y);
            _x.Points.Add(new DataPoint(x, y));
            while (_x.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _x.Points.RemoveAt(0);
            }
        }
        public void AddY(double x, double y)
        {
            y = _filterY.Next(y);
            _y.Points.Add(new DataPoint(x, y));
            while (_y.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _y.Points.RemoveAt(0);
            }
        }
        public void AddZ(double x, double y)
        {
            y = _filterZ.Next(y);
            _z.Points.Add(new DataPoint(x, y));
            while (_z.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _z.Points.RemoveAt(0);
            }
        }

        public void ClearXYZ()
        {
            _x.Points.Clear();
            _y.Points.Clear();
            _z.Points.Clear();
        }

        public string SerializeXYZ()
        {
            return JsonConvert.SerializeObject(new List<DataPoint>[3] { _x.Points, _y.Points, _z.Points });
        }

        public int DeserializeXYZ(string serialized)
        {
            try
            {
                List<DataPoint>[] result = JsonConvert.DeserializeObject<List<DataPoint>[]>(serialized);
                ClearXYZ();
                _x.Points.AddRange(result[0]);
                _y.Points.AddRange(result[1]);
                _z.Points.AddRange(result[2]);

                return (int)_z.Points.Max(p => p.X);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
