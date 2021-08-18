﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Sharp7;

namespace KeyenceDataProcessing
{
    class SimaticCommunication
    {
        public bool Opened
        {
            get { return IsOpened(); }
        }
        public CommunicationOptions CommunicationOptions { get; set; }
        public CommunicationData CommunicationData
        { 
            get 
            {
                return _communicationData;
            }
            set
            {
                Monitor.Enter(_lockObject);
                try
                {
                    _communicationData = value;
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }
            } 
        }

        private Thread _thread = null;
        private volatile bool _terminate = true;
        private S7Client _s7Client = null;
        private CommunicationOptions _startedCommuncationOptions;
        private CommunicationData _communicationData;
        private readonly object _lockObject = new Object();
 
        public void Start()
        {
            Stop();

            if (Open())
            {
                ThreadStart threadStart = new ThreadStart(this.Run);
                _thread = new Thread(threadStart);
                _terminate = false;
                _thread.Start();
            }
            Console.Out.WriteLine("Opened: " + Opened);
        }


        public void Stop()
        {
            _terminate = true;
            _thread = null;

            Close();
        }


        public void Run()
        {
            Console.Out.WriteLine("Thread started");
            CommunicationData data;
            while (!_terminate)
            {
                Monitor.Enter(_lockObject);
                try
                {
                    data = _communicationData;
                    Write(ref data);
                    Read();
                }
                finally
                {
                    Monitor.Exit(_lockObject);
                }

                Thread.Sleep(1000);
            }
            Console.Out.WriteLine("Thread stopped");
        }


        private void Read()
        {
            int block = _startedCommuncationOptions.Block;
            float y = ReadReal(block, _startedCommuncationOptions.ResultYAddress);
            Console.Out.WriteLine("y=" + y);
            float z = ReadReal(block, _startedCommuncationOptions.ResultZAddress);
            Console.Out.WriteLine("z=" + z);
            bool q = ReadBool(block, _startedCommuncationOptions.QualityAddress);
            Console.Out.WriteLine("q=" + q);
            int counter = ReadInt16(block, _startedCommuncationOptions.CounterAddress);
            Console.Out.WriteLine("counter=" + counter);
        }


        private void Write(ref CommunicationData data)
        {
            bool q = true;// data.Quality;
            float y = 1;
            float z = 2;
            Int16 count = 3;
            int block = _startedCommuncationOptions.Block;
            WriteReal(block, _startedCommuncationOptions.ResultYAddress, y);//(float)data.ResultY);
            WriteReal(block, _startedCommuncationOptions.ResultZAddress, z);//(float)data.ResultZ);
            WriteBool (block, _startedCommuncationOptions.QualityAddress, q);//(bool)data.Quality);
            WriteInt16(block, _startedCommuncationOptions.CounterAddress, count);//(Int16)data.Counter);
        }


        private bool Open()
        {
            if (Opened)
            {
                Close();
            }

            _startedCommuncationOptions = CommunicationOptions;
            try
            {
                if (CheckCommuncationOptions(_startedCommuncationOptions))
                {
                    _s7Client = OpenImpl();
                }
            }
            catch(Exception ex)
            {
                Console.Out.WriteLine(ex);
            }
 
            return Opened;
        }

        private S7Client OpenImpl()
        {
            S7Client client = new S7Client();
            int result = client.ConnectTo(
                _startedCommuncationOptions.Ip,
                _startedCommuncationOptions.Rack,
                _startedCommuncationOptions.Slot);
            if (result == 0)
            {
                return client;
            }
            return null;
        }


        private void Close()
        {
            if (IsOpened())
            {
                try
                {
                    _s7Client.Disconnect();
                }
                catch
                {
                }
                _s7Client = null;
            }
        }


        public bool IsOpened()
        {
            return _s7Client == null ? false : _s7Client.Connected;
        }


        private bool CheckCommuncationOptions(CommunicationOptions options)
        {
             return CheckRack(options.Rack)
                && CheckSlot(options.Slot)
                && options.Ip != null
                && options.Ip.Length != 0
                && CheckValueAddress(options.ResultYAddress)
                && CheckValueAddress(options.ResultZAddress)
                && CheckValueAddress(options.QualityAddress)
                && CheckValueAddress(options.CounterAddress);
        }


        private bool CheckRack(int rack)
        {
            return rack >= 0;
        }


        private bool CheckSlot(int slot)
        {
            return slot >= 0;
        }


        private bool CheckValueAddress(int address)
        {
            return address >= 0;
        }


        private bool CheckBlockNumber(int blockNumber)
        {
            return blockNumber > 10;
        }


        private byte[] ReadValue(int blockNumber, int address, int size)
        {
            byte[] buff = new byte[size];
            _s7Client.DBRead(blockNumber, address, size, buff);
            return buff;
        }


        private bool ReadBool(int blockNumber, int address)
        {
            byte[] buff = ReadValue(blockNumber, address, 1);
            return buff[0] != 0;
        }


        private Int16 ReadInt16(int blockNumber, int address)
        {
            byte[] buff = ReadValue(blockNumber, address, 2);
            return (Int16)S7.GetIntAt(buff, 0);
        }


        private Int32 ReadInt32(int blockNumber, int address)
        {
            byte[] buff = ReadValue(blockNumber, address, 4);
            return S7.GetDIntAt(buff, 0);
        }


        private float ReadReal(int blockNumber, int address)
        {
            byte[] buff = ReadValue(blockNumber, address, 4);
            return S7.GetRealAt(buff, 0);
        }


        private void WriteValue(int blockNumber, int address, byte[] buff)
        {
            _s7Client.DBWrite(blockNumber, address, buff.Length, buff);
        }


        private void WriteBool(int blockNumber, int address, bool value)
        {
            byte[] buff = new byte[1];
            S7.SetByteAt(buff, 0, (byte)(value ? 1 : 0));
            WriteValue(blockNumber, address, buff);
        }


        private void WriteInt16(int blockNumber, int address, Int16 value)
        {
            byte[] buff = new byte[2];
            S7.SetIntAt(buff, 0, value);
            WriteValue(blockNumber, address, buff);
        }


        private void WriteInt32(int blockNumber, int address, Int32 value)
        {
            byte[] buff = new byte[4];
            S7.SetDIntAt(buff, 0, value);
            WriteValue(blockNumber, address, buff);
        }


        private void WriteReal(int blockNumber, int address, float value)
        {
            byte[] buff = new byte[4];
            S7.SetRealAt(buff, 0, value);
            WriteValue(blockNumber, address, buff);
        }
    }

    internal struct CommunicationOptions
    {
        internal string Ip;
        internal int Rack;
        internal int Slot;
        internal int Block;
        internal int ResultYAddress;
        internal int ResultZAddress;
        internal int QualityAddress;
        internal int CounterAddress;

        internal void Init()
        {
            Ip = "";
            Rack = -1;
            Slot = -1;
            Block = -1;
            ResultYAddress = -1;
            ResultZAddress = -1;
            QualityAddress = -1;
            CounterAddress = -1;
        }
    }


    internal struct CommunicationData
    {
        internal double ResultY;
        internal double ResultZ;
        internal bool Quality;
        internal int Counter;

        void Init()
        {
            ResultY = 0;
            ResultZ = 0;
            Quality = false;
            Counter = 0;
        }
    }


    internal class CommunicationException : Exception
    {
    }
}
