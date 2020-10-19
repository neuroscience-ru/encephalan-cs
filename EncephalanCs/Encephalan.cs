using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace EncephalanCs
{
    public static class Encephalan
    {
        public const UInt32 M_ASK_EEG = 0x0001;
        public const UInt32 M_ASK_ALL = 0x000c;
        public const UInt32 M_ASK_HD = 0x000e;

        public const UInt32 M_ANSWER_INFO = 0x0002;
        public const UInt32 M_ANSWER_STARTSTOP = 0x0006;
        public const UInt32 M_ANSWER_CHANNELS = 0x0008;
        public const UInt32 M_ANSWER_MARKER_FP = 0x0009;
        public const UInt32 M_ANSWER_MARKER_ABC = 0x000a;

		public const string DEFAULT_ADDR = "127.0.0.1";
		public const int DEFAULT_PORT = 120;

		public const int Frequency = 250;

        private static TcpClient tcp_client;

        public delegate void OnData(int packet_num, short[] channels);
        public static event OnData OnDataEvent;

        public class SessionInfoStruct
        {
            public string ClientName;
            public int ChannelsNum;
            public ChannelInfoStruct[] ChannelsInfo;
            public string SchemeName;
        }

        public struct ChannelInfoStruct
        {
            public string Name;
            public double BitWeight;
            public double Frequency;
            public string UnitName;
            public double HighFilter;
            public double LowFilter;
            public int FilterLevel;
            public double Rejector;
            public double Sensivity;
        };

        public static SessionInfoStruct SessionInfo;

        static Encephalan()
        {
            tcp_client = new TcpClient();
        }

        public static void Connect(string addr, int port)
        {
            try
            {
                tcp_client.Connect(IPAddress.Parse(addr), port);
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10061)
                    throw new Exception("Энцефалан не готов, откройте обработку \"Доступ к данным\"");
            }
            if (!tcp_client.Connected)
                throw new Exception("Encephalan connect failed for unknown reason");

			GetSessionInfo();
        }

        public static void Connect()
        {
            Connect(DEFAULT_ADDR, DEFAULT_PORT);
        }

        public static void Disconnect()
        {
            tcp_client.Close();
        }

        private static void SendInternal(byte[] data)
        {
            tcp_client.GetStream().Write(data, 0, data.Length);
        }

        public static void Send(UInt32 type, params int[] what)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream); // для упрощения записи разных типов

            // резервирование места под размер данных
            writer.Write(0x0000);

            // отправка типа
            writer.Write(type);

            // отправка дополнительных данных
            for (int i = 0; i < what.Length; i++)
                writer.Write(what[i]);

            var data = stream.ToArray();
            Array.Copy(BitConverter.GetBytes((UInt32)data.Length - 4), 0, data, 0, 4); // записали длину

            Console.WriteLine("send: " + BitConverter.ToString(data));

            SendInternal(data); // раньше было в два этапа - длина и тело
        }


        // хелпер - читает строку в win-1251, в начале длина в байтах
        public static string ReadString1251(this BinaryReader reader)
        {
            int len = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(len);
            string s = Encoding.GetEncoding("windows-1251").GetString(bytes);
            return s;
        }

        public static string ReadString1251(this byte[] buf, int pos)
        {
            var len = BitConverter.ToUInt32(buf, pos + 0);
            var buf2 = new byte[len];
            Array.Copy(buf, pos + 4, buf2, 0, len);
            string s = Encoding.GetEncoding("windows-1251").GetString(buf2);
            return s;
        }


		public static double RawToValue(short raw, int channel_num)
		{
			return SessionInfo != null ? raw * SessionInfo.ChannelsInfo[channel_num].BitWeight : -1;
		}

		public static string RawToString(short raw, int channel_num)
		{
			return SessionInfo != null ? RawToValue(raw, channel_num) + " " + SessionInfo.ChannelsInfo[channel_num].UnitName : "n/a";
		}


        public static void GetSessionInfo()
        {
            Send(M_ASK_ALL);

            var buf = new byte[1000];
            var res = tcp_client.GetStream().Read(buf, 0, buf.Length);

			try // todo fix
			{
				File.WriteAllBytes(@"logs\start_header.dat", buf);
			}
			catch
			{
			}

			SessionInfo = new SessionInfoStruct();

            var stream = new MemoryStream(buf);
            var reader = new BinaryReader(stream);

            uint size_dummy = reader.ReadUInt32();
            uint type = reader.ReadUInt32();
            if (type != M_ANSWER_INFO)
                throw new Exception("strange answer: " + type);
            SessionInfo.ClientName = reader.ReadString1251();
            SessionInfo.ChannelsNum = reader.ReadInt32();
            SessionInfo.ChannelsInfo = new ChannelInfoStruct[SessionInfo.ChannelsNum];
            for (int i = 0; i < SessionInfo.ChannelsNum; i++)
            {
                SessionInfo.ChannelsInfo[i].Name = reader.ReadString1251();
                SessionInfo.ChannelsInfo[i].BitWeight = reader.ReadDouble();
                SessionInfo.ChannelsInfo[i].Frequency = reader.ReadDouble();
                SessionInfo.ChannelsInfo[i].UnitName = reader.ReadString1251();
                SessionInfo.ChannelsInfo[i].HighFilter = reader.ReadDouble();
                SessionInfo.ChannelsInfo[i].LowFilter = reader.ReadDouble();
                SessionInfo.ChannelsInfo[i].FilterLevel = reader.ReadInt32();
                SessionInfo.ChannelsInfo[i].Rejector = reader.ReadDouble();
                SessionInfo.ChannelsInfo[i].Sensivity = reader.ReadDouble();
            }
            SessionInfo.SchemeName = reader.ReadString1251();

            //Console.WriteLine(stream.Position);

            //           Console.WriteLine(name.Length);

        }

		private static List<short[]> loaded_data;
		public static List<short[]> LoadTxt(string path)
		{
			StreamReader file = new StreamReader(path,Encoding.GetEncoding("windows-1251"));

			// заполняем сессию
			/*
			Иванов Петр Сергеевич	<Ф.И.О.>
			м	<Пол.>
			42	<Возраст, лет>
				<Примечания>
			4.8.2019	<Дата проведения исследования: день, месяц, год>
			2	<Общее количество каналов>
			250.	<Частота дискретизации, Гц>

			3	<Количество групп физиологических сигналов>

			Т	<Тип сигналов 1-й группы>
			1	<Количество сигналов 1-й группы>
			0,0013	<Вес бита сигналов 1-й группы>
			°С	<Единицы измерения сигналов 1-й группы>
			
			Двигательная активность	<Тип сигналов 2-й группы>
			1	<Количество сигналов 2-й группы>
			0,0003	<Вес бита сигналов 2-й группы>
			у.е.	<Единицы измерения сигналов 2-й группы>

			ЭЭГ	<Тип сигналов 3-й группы>
			0	<Количество сигналов 3-й группы>
			0,1250	<Вес бита сигналов 3-й группы>
			мкВ	<Единицы измерения сигналов 3-й группы>

				<Полярность значений сигналов ЭЭГ – в соответствии с конвенцией о полярности. Негативность отражена минусом.>
			91e0d8ec-db86-4711-9994-9e1bd4ee2db9	<Идентификатор исследования>
			1	<Количество фрагментов записи>
			20:28:05.320	<Время начала 1-го фрагмента>
			2700	<Продолжительность 1-го фрагмента в отсчетах, отсчетов>
			Температура	ДПТ
			*/

			// todo: не очень, точки будут считаться как спецсигналы
			string ReadParam(StreamReader f, string param_name)
			{
				var line = f.ReadLine();
				var splitted = line.Split('\t');
				//if (!Regex.Match(splitted[1],"^<" + param_pattern + ">$").Success)
				if (splitted[1] != "<" + param_name + ">")
					throw new Exception("param " + param_name + " not match: " + splitted[1]);
				return splitted[0];
			};

			double ReadDouble(StreamReader f, string param_name)
			{
				var res = ReadParam(f, param_name);
				return double.Parse(res.Replace('.', ',')); // туду - некрасивый хак локали
			}

			SessionInfo = new SessionInfoStruct();

			SessionInfo.ClientName = ReadParam(file,"Ф.И.О.");
			ReadParam(file, "Пол.");
			ReadParam(file, "Возраст, лет");
			ReadParam(file, "Примечания");
			ReadParam(file, "Дата проведения исследования: день, месяц, год");
			SessionInfo.ChannelsNum = int.Parse(ReadParam(file, "Общее количество каналов"));
			var freq = ReadDouble(file, "Частота дискретизации, Гц");
			var groups_num = int.Parse(ReadParam(file, "Количество групп физиологических сигналов"));

#if !OLDNET
			var channels_raw = new List<(double,string)>();
			for (int i = 0; i < groups_num; i++)
			{
				//Т	<Тип сигналов 1-й группы>
				//4	<Количество сигналов 1-й группы>
				//0,0013	<Вес бита сигналов 1-й группы>
				//°С	<Единицы измерения сигналов 1-й группы>

				var ig = (i + 1) + "-й группы";
				var type = ReadParam(file, "Тип сигналов " + ig);
				var num = int.Parse(ReadParam(file, "Количество сигналов " + ig));
				var weight = ReadDouble(file, "Вес бита сигналов " + ig);
				var unit = ReadParam(file, "Единицы измерения сигналов " + ig);
				for (int j = 0; j < num; j++)
					channels_raw.Add((weight, unit));
			};
			ReadParam(file, "Полярность значений сигналов ЭЭГ – в соответствии с конвенцией о полярности. Негативность отражена минусом.");
			ReadParam(file, "Идентификатор исследования");
			ReadParam(file, "Количество фрагментов записи");
			ReadParam(file, "Время начала 1-го фрагмента");
			ReadParam(file, "Продолжительность 1-го фрагмента в отсчетах, отсчетов");

			var channels_names = file.ReadLine().Split('\t');

			SessionInfo.ChannelsInfo = new ChannelInfoStruct[SessionInfo.ChannelsNum];
			for (int i = 0; i < SessionInfo.ChannelsNum; i++)
			{
				SessionInfo.ChannelsInfo[i].Name = channels_names[i];
				SessionInfo.ChannelsInfo[i].BitWeight = channels_raw[i].Item1;
				SessionInfo.ChannelsInfo[i].Frequency = freq;
				SessionInfo.ChannelsInfo[i].UnitName = channels_raw[i].Item2;
				SessionInfo.ChannelsInfo[i].HighFilter = 100;
				SessionInfo.ChannelsInfo[i].LowFilter = 0;
				SessionInfo.ChannelsInfo[i].FilterLevel = 2;
				SessionInfo.ChannelsInfo[i].Rejector = 50;
				SessionInfo.ChannelsInfo[i].Sensivity = 1;
			}
			SessionInfo.SchemeName = "Emulated";

			var loaded_list = new List<short[]>();
			string ln;
			while ((ln = file.ReadLine()) != null)
			{
				if (ln == "<Все служебные маркеры>")
					break;
				var s_arr = ln.Split('\t');
				var arr = new short[SessionInfo.ChannelsNum];
				for (int i = 0; i < arr.Length; i++)
					arr[i] = short.Parse(s_arr[i]);
				loaded_list.Add(arr);
			}
			file.Close();
			file.Dispose();

			return loaded_list;
#else
			throw new NotImplementedException();
#endif

		}

        public static void ConnectEmulation(string path)
        {
			loaded_data = LoadTxt(path);
        }

        public static short[] EmulateAsync()
        {
            int packet_num = 0;
            while (true)
            {
				//data_arr[0] = (short)rnd.Next(-200, 200);
				//data_arr[0] = l[packet_num];
				var data_arr = loaded_data[packet_num];

                OnDataEvent?.Invoke(packet_num, data_arr);
                System.Threading.Thread.Sleep(1000 / Frequency);
                packet_num++;
                if (packet_num >= loaded_data.Count)
                    packet_num = 0;
            }
        }

        //public static int last_incoming_size = 0;
        public static void ProcessAsync()
        {
            try // todo: при выходе возникает исключение, корректить
            {
                const int buf_size = 1000;
                var buf = new byte[buf_size];
                while (true)
                {
                    var sz = tcp_client.GetStream().Read(buf, 0, buf.Length);
                    //last_incoming_size = sz;
                    //if (sz != 24)
                    //    Console.WriteLine("not common size:" + sz + ":" + BitConverter.ToString(buf, 0, sz));

                    int pos = 0;
                    while (true) // может прийти сразу несколько пакетов, смотрим
                    {
                        var len = BitConverter.ToInt32(buf, pos + 0);
                        var type = BitConverter.ToUInt32(buf, pos + 4);

                        if (type == M_ANSWER_CHANNELS) // пакет данных
                            DecodeChannels(buf, pos + 8);
                        else if (type == M_ANSWER_STARTSTOP) // начало или остановка съема
                            DecodeStartStop(buf, pos + 8);
                        else if (type == M_ANSWER_MARKER_FP) // маркер (в т.ч. в начале записи)
                            DecodeMarkerFP(buf, pos + 8);
                        else if (type == M_ANSWER_MARKER_ABC) // маркер (в т.ч. в начале записи)
                            DecodeMarkerABC(buf, pos + 8);
                        else
                            throw new Exception("strange incoming type: " + type);

                        pos += len + 4;
                        if (pos >= sz)
                            break;
                        if (pos >= buf_size)
                        {
                            Console.WriteLine("packets lost");
                            break;
                        }
                    }
                }
            }
            catch (IOException) { };
        }

        public static void DecodeChannels(byte[] buf, int pos)
        {
            // номер среза
            // количество каналов
            // размер диапазона, всегда 1, кроме килогерцовых данных
            // каналы

            var packet_num = BitConverter.ToInt32(buf, pos + 0);
            var cur_channels_count = BitConverter.ToInt32(buf, pos + 4);
            if (cur_channels_count != SessionInfo.ChannelsNum)
                throw new Exception("channel num mismatch");
            // var diap_dummy = BitConverter.ToInt32(buf, pos + 8);
            var data_arr = new short[cur_channels_count];

            int data_pos = pos + 12;
			for (int i = 0; i < cur_channels_count; i++)
				try
				{
					data_arr[i] = BitConverter.ToInt16(buf, data_pos + i * 2);
				}
				catch (Exception)
				{
					var s = pos + ": " + BitConverter.ToString(buf);
					File.AppendAllText("exception.log",s);
					throw;
				}

            OnDataEvent?.Invoke(packet_num, data_arr);
        }

        public static void DecodeStartStop(byte[] buf, int pos)
        {
            var startstop = BitConverter.ToUInt32(buf, pos + 0);
            if (startstop == 1)
                Console.WriteLine("1 - съем начат");
            else if (startstop == 0)
                Console.WriteLine("0 - съем остановлен");
            else
                throw new Exception("unknown type: " + startstop);
            return;
        }

        public static void DecodeMarkerFP(byte[] buf, int pos)
        {
            var packet_num = BitConverter.ToInt32(buf, pos + 0);
            var name = buf.ReadString1251(pos + 4);
            Console.WriteLine("marker fp: " + packet_num + ":" + name);
        }

        public static void DecodeMarkerABC(byte[] buf, int pos)
        {
            var packet_num = BitConverter.ToInt32(buf, pos + 0);
            var name = buf.ReadString1251(pos + 4);
            Console.WriteLine("marker abc: " + packet_num + ":" + name);
        }

    }
}
