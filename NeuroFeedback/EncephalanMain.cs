using NAudio.Midi;
using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Tools;
using EncephalanCs;

namespace NeuroFeedback
{
    public partial class EncephalanMain : Form
    {
        public EncephalanMain()
        {
            InitializeComponent();
        }

        private PanelChart pc;
        private FFT fft;
        //private FFT fft2;

        const double FREQ_MIN_DELTA = 0;
        const double FREQ_MAX_DELTA = 4;
        const double FREQ_MIN_THETA = FREQ_MAX_DELTA;
        const double FREQ_MAX_THETA = 8;
        const double FREQ_MIN_ALPHA = FREQ_MAX_THETA;
        const double FREQ_MAX_ALPHA = 13;
        const double FREQ_MIN_BETA = FREQ_MAX_ALPHA;
        const double FREQ_MAX_BETA = 30;

        public static class Consts
        {
            public static EncephalanMain LINK_TO_FORM;
        }

        private async void Form1_Shown(object sender, EventArgs e)
        {
            Consts.LINK_TO_FORM = this;

            pc = new PanelChart(pnChart);

            var addr = "127.0.0.1";
            var port = 120;

            try
            {
                Encephalan.Connect(addr, port);
            }
            catch (Exception ex)
            {
				var emu_path = "samples/cq.txt";
				MessageBox.Show(ex.Message + "\n\n" + "Будет запущена эмуляция " + emu_path);
                Encephalan.ConnectEmulation(emu_path);
                //Close();
            }

            fft = new FFT();
            fft.OnFFTEvent += OnFFT;

            //fft2 = new FFT();
            //fft2.OnFFTEvent += OnFFT2;
            //fft2.ChannelNum = 1;

            InitData();
            InitChannels();
            InitCharts();

            Misc.Init(chMisc);
            MiscBands.Init(chMisc);

            Encephalan.OnDataEvent += OnData;

			pnChart.Refresh();


			//PerfMonitor.Start();
			//PerfMonitor.StartLogging(String.Format("perf_{0}.txt", DateTime.Now.ToString("yyyyMMdd_HHmmss")));

			if (Encephalan.SessionInfo.SchemeName == "Emulated")
                await Task.Factory.StartNew(() => Encephalan.EmulateAsync(), TaskCreationOptions.LongRunning);


            //await Task.Run(() => Encephalan.GetData());
            await Task.Factory.StartNew(() => Encephalan.ProcessAsync(), TaskCreationOptions.LongRunning);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Encephalan.Disconnect();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Close();
        }


        private void rbFFTSize_CheckedChanged(object sender, EventArgs e)
        {
            fft.Size = Convert.ToInt32(((RadioButton)sender).Tag);
            //fft2.Size = Convert.ToInt32(((RadioButton)sender).Tag);
            AskForFit();
        }

        private void rbFFTStep_CheckedChanged(object sender, EventArgs e)
        {
            fft.Step = Convert.ToInt32(((RadioButton)sender).Tag);
            //fft2.Step = Convert.ToInt32(((RadioButton)sender).Tag);
        }

        private void rbAverage_CheckedChanged(object sender, EventArgs e)
        {
            Average = Convert.ToInt32(((RadioButton)sender).Tag);
        }

        private void rbSpectrumType_CheckedChanged(object sender, EventArgs e)
        {
            fft.SpectrumType = (FFT.SpectrumTypeEnum)Convert.ToInt32(((RadioButton)sender).Tag);
            //fft2.SpectrumType = (FFT.SpectrumTypeEnum)Convert.ToInt32(((RadioButton)sender).Tag);
            AskForFit();
        }

        private void rbChannelNum_CheckedChanged(object sender, EventArgs e)
        {
            fft.ChannelNum = Convert.ToInt32(((RadioButton)sender).Tag);
        }

        string misc_type = "bands";
        private void rbMiscType_CheckedChanged(object sender, EventArgs e)
        {
            misc_type = (string)((RadioButton)sender).Tag;
            switch (misc_type)
            {
                case "bands": MiscBands.Init(chMisc); break;
                case "freq": MiscFrequency.Init(chMisc); break;
                default: throw new Exception("strange type: " + misc_type);
            }
        }

        private void btFit_Click(object sender, EventArgs e)
        {
            FitFFT();
        }

        // tool functions ------------------------------------------------

        private void AddStrip(double from, double to, Color color)
        {
            var ax = chFFT.ChartAreas[0].AxisX;
            var sl = new StripLine();
            sl.BackColor = color;
            sl.IntervalOffset = from;
            sl.StripWidth = to - from;
            ax.StripLines.Add(sl);
        }

        private void InitCharts()
        {
            chFFT.ChartAreas[0].AxisX.Minimum = 0;
            chFFT.ChartAreas[0].AxisX.Maximum = 35;

            AddStrip(FREQ_MIN_DELTA, FREQ_MAX_DELTA, Color.FromArgb(255, 245, 245)); // red
            AddStrip(FREQ_MIN_THETA, FREQ_MAX_THETA, Color.FromArgb(255, 255, 230)); // yellow
            AddStrip(FREQ_MIN_ALPHA, FREQ_MAX_ALPHA, Color.FromArgb(230, 255, 230)); // green
            AddStrip(FREQ_MIN_BETA, FREQ_MAX_BETA, Color.FromArgb(230, 255, 255)); // blue

            //            chFFT.ChartAreas[0].AxisX.RoundAxisValues();
        }

        private void AddRadioButton(int num, string text, int x, int y, int w, int h)
        {
            var rb = new RadioButton();
            rb.Appearance = System.Windows.Forms.Appearance.Button;
            rb.Location = new System.Drawing.Point(x, y);
            rb.Name = "rbChannel" + num;
            rb.Size = new System.Drawing.Size(w, h);
            rb.TabIndex = 15;
            rb.Tag = num.ToString();
            rb.Text = text;
            rb.UseVisualStyleBackColor = true;
            rb.CheckedChanged += new System.EventHandler(this.rbChannelNum_CheckedChanged);
            pnChannels.Controls.Add(rb);

        }
        private void InitChannels()
        {
            var x = rbChannel0.Location.X;
            var y0 = rbChannel0.Location.Y;
            var w = rbChannel0.Size.Width;
            var h = rbChannel0.Size.Height;
            var yspace = rbChannel1.Location.Y - rbChannel0.Location.Y;
            for (int i = 0; i < Encephalan.SessionInfo.ChannelsNum; i++)
            {
                AddRadioButton(i, Encephalan.SessionInfo.ChannelsInfo[i].Name, x, y0 + i * yspace, w, h);
            }
            pnChannels.Controls.Remove(rbChannel0);
            pnChannels.Controls.Remove(rbChannel1);

            var first = pnChannels.Controls.OfType<RadioButton>().First();
            first.Checked = true;
        }

        public class FFT
        {
            DSPLib.FFT fft;

            public enum SpectrumTypeEnum { Amplitude = 0, Power = 1 };

            public int ChannelNum = 1;

            private int _size;
            public int Size {
                get => _size;
                set {
                    _size = value;
                    fft = new DSPLib.FFT();
                    fft.Initialize((uint)_size);
                    Freqs = fft.FrequencySpan(Encephalan.Frequency);
                }
            }
            public int Step;
            public SpectrumTypeEnum SpectrumType = SpectrumTypeEnum.Power;

            private long lastrun_n = 0;

            public delegate void OnFFT(double[] freqs, double[] mags);
            public event OnFFT OnFFTEvent;

            public double[] Freqs;

            public FFT()
            {
                Size = 1024;
                Step = 50;
            }

            public static (int, int) CalcBin(double min_freq, double max_freq, double[] freqs)
            {
                int min_bin = -1;
                int max_bin = -1;
                var sz = freqs.Length / 2;
                for (int i = 0; i < sz; i++)
                {
                    if (min_bin == -1 && freqs[i] >= min_freq)
                        min_bin = i;

                    if (freqs[i] > max_freq)
                    {
                        max_bin = i - 1;
                        break;
                    }


                }
                return (min_bin, max_bin);
            }

            public static double BinSumm(double min_freq, double max_freq, double[] freqs, double[] mags)
            {
                double res = 0;
                (var min_bin, var max_bin) = CalcBin(min_freq, max_freq, freqs);
                for (int i = min_bin; i <= max_bin; i++)
                    res += mags[i];
                return res;
            }
                

            public void OnData(int packet_num, short[] data_arr)
            {
                var lock_obj = new object();

                lock (lock_obj)
                {
                    // чтобы потом не менялось, иначе мультипоточность валит
                    var fft_size_internal = Size;
                    var fft_step_internal = Step;

                    if (N > fft_size_internal && N > lastrun_n + fft_step_internal)
                    {
                        int pos = (int)(N - (fft_size_internal - 1));
                        CalcFFT(total_arr[ChannelNum], pos, fft_size_internal, SpectrumType);
                        lastrun_n = N;
                    }

                }

            }

            private void CalcFFT(short[] a_arr, int a_pos, int a_size, FFT.SpectrumTypeEnum a_spectrumtype)
            {
                var fft_arr = new double[a_size];
                Array.Copy(a_arr, a_pos, fft_arr, 0, a_size);
                var fft_arr_double = Array.ConvertAll(fft_arr, Convert.ToDouble);

                var spectrum = fft.Execute(fft_arr_double);

                var mags = (a_spectrumtype == SpectrumTypeEnum.Amplitude)
                    ? DSPLib.DSP.ConvertComplex.ToMagnitude(spectrum)
                    : DSPLib.DSP.ConvertComplex.ToMagnitudeSquared(spectrum);

                OnFFTEvent?.Invoke(Freqs, mags);

            }


        }

        private void OnData(int packet_num, short[] data_arr)
        {
            for (int i = 0; i < Encephalan.SessionInfo.ChannelsNum; i++)
                total_arr[i][N] = data_arr[i];

            fft.OnData(packet_num, data_arr);
            //fft2.OnData(packet_num, data_arr);

            pc.DrawNext(data_arr[fft.ChannelNum]);

            BeginInvoke((Action)(() =>
            {
                Text = "cpu: " + PerfMonitor.ToString();
                //Text = "data: " + N
                //    + " (" + Encephalan.last_incoming_size + ")"
                //    + ": " + String.Join(" - ", data_arr)
            ;
            }));

            N++;
        }

        public int Average = 2; // 0, 1, 2
        private void OnFFT(double[] freqs, double[] mags)
        {
            BeginInvoke((Action)(() =>
            {
                DrawFFT(chFFT.Series[0].Points, freqs, mags);
                DrawFFTAverage(chFFT.Series[1].Points, freqs, mags, Average);
                chFFT.Update();

                switch (misc_type)
                {
                    case "bands": MiscBands.Draw(freqs, mags); break;
                    case "freq": MiscFrequency.Draw(freqs, mags); break;
                    default: throw new Exception("strange type: " + misc_type);
                }

                Biofeedback.Draw(freqs, mags);

                chMisc.Update();

                if (ask_for_fit)
                {
                    FitFFT();
                    ask_for_fit = false;
                }

            }));

        }

        /*private void OnFFT2(double[] freqs, double[] mags)
        {
            BeginInvoke((Action)(() =>
            {
                DrawFFT(chFFT.Series[2].Points, freqs, mags);
                chFFT.Update();
            }));

        }*/


        private static Int16[][] total_arr;
        public static long N = 0;
        private void InitData()
        {
            total_arr = new short[Encephalan.SessionInfo.ChannelsNum][];
            for (int i = 0; i < Encephalan.SessionInfo.ChannelsNum; i++)
                total_arr[i] = new short[Encephalan.Frequency * 3600];
        }

        public void DrawFFT(DataPointCollection points, double[] freqs, double[] mags)
        {
            points.Clear();
            for (int i = 0; i < mags.Length; i++)
                points.AddXY(freqs[i], mags[i]);
        }

        public void DrawFFTAverage(DataPointCollection points, double[] freqs, double[] mags, int a_average)
        {
            points.Clear();

            // корректируем начало, чтобы там тоже были точки
            if (a_average == 1)
                points.AddXY(freqs[0], (mags[0] + mags[1]) / 2);
            if (a_average == 2)
            {
                points.AddXY(freqs[0], (mags[0] + mags[1] + mags[2]) / 3);
                points.AddXY(freqs[1], (mags[0] + mags[1] + mags[2] + mags[4]) / 4);
            }

            for (int i = a_average; i < mags.Length - a_average; i++)
            {
                double avg_val;
                switch (a_average)
                {
                    case 0:
                        avg_val = mags[i];
                        break;
                    case 1:
                        avg_val = (mags[i] + mags[i - 1] + mags[i + 1]) / 3;
                        break;
                    case 2:
                        avg_val = (mags[i] + mags[i - 1] + mags[i + 1] + mags[i - 2] + mags[i + 2]) / 5;
                        break;
                    default:
                        throw new Exception("strange average: " + a_average);
                }
                points.AddXY(freqs[i], avg_val);
            }
        }

        static class Misc
        {
            static Chart chart;

            public static void Init(Chart a_chart)
            {
                chart = a_chart;
            }

            public static void Clear()
            {
                foreach (var series in chart.Series)
                    series.Points.Clear();
            }

            public static Series AddSeries(string a_name, Color a_color, bool axis_right)
            {
                var sr = new Series();
                sr.Name = a_name;
                sr.ChartArea = "ChartArea1";
                sr.ChartType = SeriesChartType.Line;
                sr.Color = a_color;
                if (axis_right)
                    sr.YAxisType = AxisType.Secondary;
                chart.Series.Add(sr);
                return sr;
            }

        }

        // сразу считать смысла нет, потому что данные не обновились.
        private bool ask_for_fit = false;
        public void AskForFit()
        {
            ask_for_fit = true;
        }

        public void FitFFT()
        {
            var points = chFFT.Series[0].Points;
            var mag_max = -100.0;
            for (int i = 0; i < points.Count; i++)
                if (mag_max < points[i].YValues[0])
                    mag_max = points[i].YValues[0];
            chFFT.ChartAreas[0].AxisY.Maximum = mag_max * 2;
            Console.WriteLine("fitted to " + mag_max);
        }



        private void btBiofeedback_Click(object sender, EventArgs e)
        {
            Biofeedback.Init(chMisc, tbBiofeedback.Text);
        }


        private void btMiscClear_Click(object sender, EventArgs e)
        {
            Misc.Clear();
        }


        // ----------------------------------------------------------------------------------------
        static class MiscFrequency
        {
            static Chart chart;

            static Series s_max;
            static Series s_weighted;

            public static void Init(Chart a_chart)
            {
                chart = a_chart;

                chart.Series.Clear();

                s_max = Misc.AddSeries("Max", Color.Blue, true);
                s_weighted = Misc.AddSeries("Weighted", Color.Green, true);

                chart.ChartAreas[0].AxisY2.IsStartedFromZero = false;
                chart.ChartAreas[0].AxisY2.Minimum = double.NaN;
                chart.ChartAreas[0].AxisY2.Maximum = double.NaN;

            }

            public static void Draw(double[] freqs, double[] mags)
            {
                //double freq_max = 0;
                //double mag_max = -100;
                //const double freq_win_min = 8; // was 7
                //const double freq_win_max = 13;

                //var sz = freqs.Length / 2;
                //for (int i = 0; i < sz; i++)
                //{
                //    if (freqs[i] >= freq_win_min && freqs[i] <= freq_win_max && mags[i] > mag_max)
                //    {
                //        freq_max = freqs[i];
                //        mag_max = mags[i];
                //    }
                //}
                //chart.Series[2].Points.AddY(freq_max);


                (var min_bin, var max_bin) = FFT.CalcBin(FREQ_MIN_ALPHA, FREQ_MAX_ALPHA, freqs);

                // частота максимума
                double freq_max2 = 0;
                double mag_max2 = -100;
                for (int i = min_bin; i <= max_bin; i++)
                {
                    if (mags[i] > mag_max2)
                    {
                        freq_max2 = freqs[i];
                        mag_max2 = mags[i];
                    }
                }
                s_max.Points.AddY(freq_max2);

                // взвешенное среднее
                double mult = 0;
                double sum = 0;
                for (int i = min_bin; i <= max_bin; i++)
                {
                    mult += mags[i] * freqs[i];
                    sum += mags[i];
                }
                var weighted = mult / sum;
                s_weighted.Points.AddY(weighted);
            }


        }


        // ----------------------------------------------------------------------------------------
        static class MiscBands
        {
            static Chart chart;

            static Series s_total;
            static Series s_theta;
            static Series s_alpha;
            static Series s_beta;

            static DateTime dt_start;
            public static DateTime dt_cur; // с 1 января 2019 - для отображения на чарте

            public static void Init(Chart a_chart)
            {
                chart = a_chart;

                chart.Series.Clear();

                s_theta = Misc.AddSeries("Theta", Color.FromArgb(192,192,0), true);
                s_alpha = Misc.AddSeries("Alpha", Color.Green, true);
                s_beta = Misc.AddSeries("Beta", Color.RoyalBlue, true);
                s_total = Misc.AddSeries("Total", Color.Gray, false);

// слишком медленно
//                s_theta.Color = Color.FromArgb(30, s_theta.Color);
//                s_alpha.Color = Color.FromArgb(30, s_alpha.Color);
//                s_beta.Color = Color.FromArgb(30, s_beta.Color);

                //s_total = chart.Series[0];
                //s_theta = chart.Series[3];
                //s_alpha = chart.Series[1];
                //s_beta = chart.Series[2];

                s_total.XValueType = ChartValueType.DateTime;
                s_theta.XValueType = ChartValueType.DateTime;
                s_alpha.XValueType = ChartValueType.DateTime;
                s_beta.XValueType = ChartValueType.DateTime;

                //chart.ChartAreas[0].AxisX.LabelStyle.Format = "mm:ss";
                chart.ChartAreas[0].AxisX.LabelStyle.Format = "mm:ss";

                chart.ChartAreas[0].AxisY2.Minimum = 0;
                chart.ChartAreas[0].AxisY2.Maximum = 100;

                chart.ChartAreas[0].AxisY.Maximum = 10000; 

                dt_start = DateTime.Now;

                //s_theta.ChartType = SeriesChartType.StackedArea;
                //s_alpha.ChartType = SeriesChartType.StackedArea;
                //s_beta.ChartType = SeriesChartType.StackedArea;
            }

            public static (double, double, double, double) CalcMagDiaps(double[] freqs, double[] mags)
            {
                double mag_d = 0;
                double mag_t = 0;
                double mag_a = 0;
                double mag_b = 0;

                var sz = freqs.Length / 2;
                for (int i = 0; i < sz; i++)
                {
                    var f = freqs[i];
                    var m = mags[i];

                    if (f < FREQ_MAX_DELTA)
                        mag_d += m;
                    else if (f < FREQ_MAX_THETA)
                        mag_t += m;
                    else if (f < FREQ_MAX_ALPHA)
                        mag_a += m;
                    else if (f < FREQ_MAX_BETA)
                        mag_b += m;
                    else
                        break;
                }
                return (mag_d, mag_t, mag_a, mag_b);
            }

            public static void Draw(double[] freqs, double[] mags)
            {
                (var mag_d, var mag_t, var mag_a, var mag_b) = CalcMagDiaps(freqs, mags);
                var mag_total = mag_t + mag_a + mag_b;

                dt_cur = new DateTime(2019,1,1).Add(DateTime.Now - dt_start);

                s_total.Points.AddXY(dt_cur, mag_total);
                s_theta.Points.AddXY(dt_cur, mag_t / mag_total * 100);
                //s_alpha.Points.AddXY(dt, mag_a / (mag_a + mag_b) * 100);
                //s_beta.Points.AddY(mag_b / (mag_a + mag_b) * 100);
                s_alpha.Points.AddXY(dt_cur, mag_a / mag_total * 100);
                s_beta.Points.AddXY(dt_cur, mag_b / mag_total * 100);


                if (s_total.Points.Count > 1000)
                {
					foreach (var series in chart.Series)
						series.Points.RemoveAt(0);
                    //s_total.Points.RemoveAt(0);
                    //s_theta.Points.RemoveAt(0);
                    //s_alpha.Points.RemoveAt(0);
                    //s_beta.Points.RemoveAt(0);
                    chart.ChartAreas[0].RecalculateAxesScale(); //knowhow https://social.msdn.microsoft.com/Forums/vstudio/en-US/3db5adac-7342-494c-84a4-63f342f6abd5/c-chart-x-axis-shifting?forum=MSWinWebChart
                }

                //chart.ChartAreas[0].AxisX.Minimum = dt.ToOADate() - 30 / (24 * 60 * 60.0);
                //chart.ChartAreas[0].AxisX.Maximum = dt.ToOADate();
                //chart.ChartAreas[0].AxisX.ScaleView.


            }
        }


        static class Biofeedback
        {

            static Chart chart;

            static Series s_feedback;

            static MidiOut midiOut;
            static System.Timers.Timer sound_timer;
            static System.Timers.Timer stop_timer;

            static int cur_note;

            static bool Enabled = false;

            static double freq_1_from;
            static double freq_1_to;
            static double freq_2_from;
            static double freq_2_to;

            public static void ParseParam(string param)
            {
                var m = Regex.Match(param, @"(\d+)-(\d+)/(\d+)-(\d+)");
                if (!m.Success)
                    throw new Exception("param is not valid");
                freq_1_from = double.Parse(m.Groups[1].Value);
                freq_1_to = double.Parse(m.Groups[2].Value);
                freq_2_from = double.Parse(m.Groups[3].Value);
                freq_2_to = double.Parse(m.Groups[4].Value);
            }

            public static void Init(Chart a_chart, string param)
            {
                ParseParam(param);

                chart = a_chart;

                if (chart.Series.IsUniqueName("Feedback"))
                    s_feedback = Misc.AddSeries("Feedback", Color.Lime, true);

                sound_timer = new System.Timers.Timer(2000);
                sound_timer.AutoReset = true;
                sound_timer.Elapsed += OnTimerEvent;
                sound_timer.Enabled = true;

                stop_timer = new System.Timers.Timer(1900);
                stop_timer.AutoReset = false;
                stop_timer.Elapsed += OnStopEvent;

                if (midiOut == null)
                    midiOut = new MidiOut(0);
                midiOut.Volume = 65535;
                //var panSettingCenter = 64;
                //var cce = new ControlChangeEvent(0L, 1, MidiController.Pan, panSettingCenter);
                //midiOut.Send(cce.GetAsShortMessage());

                Enabled = true;
            }

            public static void Draw(double[] freqs, double[] mags)
            {
                if (!Enabled)
                    return;
                        
                //var val = FFT.BinSumm(6, 13, freqs, mags) / FFT.BinSumm(6, 30, freqs, mags) * 100;
                var val = FFT.BinSumm(freq_1_from, freq_1_to, freqs, mags) 
                        / FFT.BinSumm(freq_2_from, freq_2_to, freqs, mags) 
                        * 100;

                //(var mag_d, var mag_t, var mag_a, var mag_b) = CalcMagDiaps(freqs, mags);
                //var mag_total = mag_t + mag_a + mag_b;

                //var val = (mag_t + mag_a) / mag_total * 100;

                s_feedback.Points.AddXY(MiscBands.dt_cur, val);
            }

            private static void NoteOn(int num)
            {
                midiOut.Send(MidiMessage.StartNote(num, 127, 1).RawData);
            }

            private static void NoteOff(int num)
            {
                midiOut.Send(MidiMessage.StopNote(num, 0, 1).RawData);
            }

            private static void OnStopEvent(object sender, ElapsedEventArgs e)
            {
                NoteOff(cur_note);
            }

            private static void OnTimerEvent(object sender, ElapsedEventArgs e)
            {
                if (s_feedback.Points.Count == 0)
                    return;

                var val = s_feedback.Points[s_feedback.Points.Count - 1].YValues[0];

                // 5-30 hz, 1024, звук раз в секунду, обработка раз в полсекунды, 
                // звуки от 0 до 90 - на дипаазон от 20 до 100 (1.125 ноты в процент

                //var min_note = 0;
                //var max_note = 90;

                //var min_val = 20;
                //var max_val = 100;
                //var note_percent = (val - min_val) / (max_val - min_val);

                // полные диапазоны - 1.27 ноты в проценте
                //var min_val = 0;
                //var max_val = 100;

                //var min_note = 0;
                //var max_note = 127;

                //var note_percent = (val - min_val) / (max_val - min_val);

//                if (note_percent < 0 || note_percent > 100)
//                    return;

                // var note_num = min_note + (max_note - min_note) * note_percent;
                // var note_num = max_note - (max_note - min_note) * note_percent;

                // преобразовывает val1..val2 в note1..note2
                int val2note(double a_val)
                {
                    var val1 = 0;
                    var val2 = 100;
                    var note1 = 127;
                    var note2 = 0;
                    if (a_val < val1 || a_val > val2)
                        throw new Exception("wrong val2note: " + a_val); // а можно просто тихо возвращаться

                    var coef = (note2 - note1) / (val2 - val1); // -1.27
                    var note = note1 + (val - val1) * coef;

                    return (int)note;
                }

                var note_num2 = val2note(val);
                cur_note = note_num2;


                /*BeginInvoke((Action)(() =>
                {
                    Text = "df " + note_num2;
                }));*/

                NoteOn(note_num2);
                stop_timer.Enabled = true;

                // System.Threading.Thread.Sleep(200);
                //midiOut.Send(MidiMessage.StopNote(61, 0, 1).RawData);
                //       System.Threading.Thread.Sleep(1000);

            }


        }


        // ----------------------------------------------------------------------------------------

    }
}