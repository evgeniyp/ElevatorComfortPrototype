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

namespace WpfApplication1
{
    public partial class MainWindow : Window
    {
        private const int REDRAW_INTERVAL_MS = 30;

        private DispatcherTimer _graphRedrawTimer;
        private UM6LTSensorParser _parser;
        private SerialPort _serialPort;
        private MainViewModel _mainViewModel { get { return DataContext as MainViewModel; } }
        private long _startTime = DateTime.Now.Ticks;

        public MainWindow()
        {
            InitializeComponent();

            InitializeParser();
            InitializeSerialPort();
            InitializeRedrawTimer();
        }

        private void InitializeParser()
        {
            _parser = new UM6LTSensorParser();
        }

        private void InitializeSerialPort()
        {
            _serialPort = new SerialPort("COM3");

            var buffer = new byte[16384];
            _serialPort.Encoding = System.Text.Encoding.ASCII;
            _serialPort.BaudRate = 115200;
            _serialPort.Parity = Parity.None;
            _serialPort.StopBits = StopBits.One;
            _serialPort.DataBits = 8;
            _serialPort.Handshake = Handshake.None;
            _serialPort.DataReceived += (sender, eventArgs) =>
            {
                try
                {
                    SerialPort sp = (SerialPort)sender;
                    var bytesToRead = _serialPort.BytesToRead;
                    sp.Read(buffer, 0, bytesToRead);
                    _parser.HandleData(buffer, bytesToRead);
                }
                catch (Exception e) { this.Dispatcher.BeginInvoke((Action)(() => { this.Title = e.Message; })); }
            };
        }

        private void InitializeRedrawTimer()
        {
            _graphRedrawTimer = new DispatcherTimer();
            _graphRedrawTimer.Tick += (sender, e) => { _mainViewModel.Invalidate(); };
            _graphRedrawTimer.Interval = TimeSpan.FromMilliseconds(REDRAW_INTERVAL_MS);
        }

        private void EnableDataEntry(bool enable)
        {
            if (enable)
            {
                _parser.OnX = v => { this.Dispatcher.BeginInvoke((Action)(() => 
                {
                    _mainViewModel.AddX((DateTime.Now.Ticks - _startTime) / 10000000.0, v);
                })); };

                _parser.OnY = v => { this.Dispatcher.BeginInvoke((Action)(() => { 
                    _mainViewModel.AddY((DateTime.Now.Ticks - _startTime) / 10000000.0, v);
                })); };

                _parser.OnZ = v => { this.Dispatcher.BeginInvoke((Action)(() => {
                    _mainViewModel.AddZ((DateTime.Now.Ticks - _startTime) / 10000000.0, v); 
                })); };

                try
                {
                    _serialPort.Open();
                    _graphRedrawTimer.IsEnabled = true;
                }
                catch (System.IO.IOException e) { this.Title = e.Message; }
            }
            else
            {
                _parser.OnX = null;
                _parser.OnY = null;
                _parser.OnZ = null;

                try 
                {
                    _graphRedrawTimer.IsEnabled = false;
                    _serialPort.Close();
                }
                catch (System.IO.IOException e) { this.Title = e.Message; }
            }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Content.ToString() == "Start")
            {
                button.Content = "Stop";
                EnableDataEntry(true);
            }
            else
            {
                button.Content = "Start";
                EnableDataEntry(false);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EnableDataEntry(false);
        }
    }

    public class MainViewModel
    {
        private const int COMMAND_RATE = 20;
        private const int SECONDS_TO_REMEMBER = 10;
        private const int LOWPASS_FREQ = 5;

        private FilterButterworth _filter = new FilterButterworth(LOWPASS_FREQ, COMMAND_RATE, FilterButterworth.PassType.Lowpass, Math.Sqrt(2));

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
            _x.Points.Add(new DataPoint(x, y));
            while (_x.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _x.Points.RemoveAt(0);
            }
        }
        public void AddY(double x, double y)
        {
            _y.Points.Add(new DataPoint(x, y));
            while (_y.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _y.Points.RemoveAt(0);
            }
        }
        public void AddZ(double x, double y)
        {
            _filter.Update(y);
            y = _filter.Value;
            _z.Points.Add(new DataPoint(x, y));
            while (_z.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            {
                _z.Points.RemoveAt(0);
            }
        }
    }
}
