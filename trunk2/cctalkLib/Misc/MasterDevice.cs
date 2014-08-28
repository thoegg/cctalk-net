using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using dk.CctalkLib.Annotations;
using dk.CctalkLib.Messages;
using NLog;

namespace dk.CctalkLib.Misc
{
    /// <summary>
    /// Represents abstract master device (e.g. this PC) that control all slave devices connected to serial port associated with this master device
    /// </summary>
    [UsedImplicitly]
    public class MasterDevice : IDisposable
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        #region fields
        /// <summary>
        /// DefaultTimeout
        /// </summary>
        public const int DefaultTimeout = 1000;

        /// <summary>
        /// Serial port
        /// </summary>
        public SerialPort Port
        {
            [UsedImplicitly]
            set
            {
                _port = value;
                if (value != null) _portSettings = value;
            }
            get { return _port; }
        }

        /// <summary>
        /// State of master device. At one time, the master can be in only one state.
        /// </summary>
        [UsedImplicitly]
        public MasterDeviceState State { private set; get; }
        /// <summary>
        /// Address of master device (by default eq 1)
        /// </summary>
        [UsedImplicitly]
        public byte ID { set; get; }

        /// <summary>
        /// List of slave devices
        /// </summary>
        [UsedImplicitly]
        public List<SlaveDevice> SlaveDevices { private set; get; }

        private Message _lastMessage;
        /// <summary>
        /// Last message transmitted or received by master device
        /// </summary>
        [UsedImplicitly]
        public Message LastMessage
        {
            set { _lastMessage = value; RaiseLastMessageUpdatedEvent(_lastMessage); }
            get { return _lastMessage; }
        }

        #endregion

        #region events
        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public event LastMessageEventHandler LastMessageUpdated;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        public delegate void LastMessageEventHandler(object sender, LastMessageUpdatedEventArgs eventArgs);

        private void RaiseLastMessageUpdatedEvent(Message message)
        {
            if (LastMessageUpdated == null) return;
            Task.Factory.StartNew(() => LastMessageUpdated(this, new LastMessageUpdatedEventArgs { LastMessage = message }));
        }
        /// <summary>
        /// 
        /// </summary>
        public class LastMessageUpdatedEventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            [UsedImplicitly]
            public Message LastMessage { set; get; }
        }
        #endregion

        #region private members

        /// <summary>
        /// 
        /// </summary>
        private readonly object _locker = new object();

        private SerialPort _port;
        private SerialPort _portSettings;
        private readonly object _syncRoot4SerialPortActions = new object();

        #endregion

        /// <summary>
        /// .ctor
        /// </summary>
        public MasterDevice()
        {
            ID = 1;
            SlaveDevices = new List<SlaveDevice>();
        }

        private bool IsConnected { set; get; }

        /// <summary>
        /// Opens serial port. For now this operation is equal this.Port.Open();
        /// </summary>
        [UsedImplicitly]
        public void Connect()
        {
            _logger.Debug("master device connection start");
            if (IsConnected)
                return;
            lock (_syncRoot4SerialPortActions)
            {
                if (Port != null)
                {
                    if (Port.IsOpen)
                        Port.Close();
                    Port.Dispose();
                }
                Port = null;

                Port = new SerialPort
                {
                    BaudRate = _portSettings.BaudRate,
                    PortName = _portSettings.PortName,
                    StopBits = _portSettings.StopBits,
                    DataBits = _portSettings.DataBits,
                    Handshake = _portSettings.Handshake,
                    Parity = _portSettings.Parity,
                };
                Thread.Sleep(1000);
                Port.Open();

                if (Port.IsOpen)
                    IsConnected = true;
            }
            _logger.Debug("master device connection finish (isSuccess == " + IsConnected + ")");
        }

        /// <summary>
        /// Closes serial port and terminates all slave devices associated with this master device
        /// </summary>
        [UsedImplicitly]
        public void Disconnect()
        {
            _logger.Debug("master device disconnection start");
            lock (_syncRoot4SerialPortActions)
            {
                if (Port != null)
                {
                    if (Port.IsOpen)
                        Port.Close();
                    Port.Dispose();
                    Port = null;
                }
                IsConnected = false;
            }
            _logger.Debug("master device disconnection finish");
        }

        [UsedImplicitly]
        public void Dispose()
        {
            foreach (var slaveDevice in SlaveDevices)
            {
                slaveDevice.TerminateDevice();
            }
            Disconnect();
        }

        /// <summary>
        /// Add default slave device to slave device list
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [UsedImplicitly]
        public SlaveDevice AddSlaveDevice(byte id)
        {
            var slave = new SlaveDevice(id, this);
            SlaveDevices.Add(slave);
            return slave;
        }

        /// <summary>
        /// Send command to specific device
        /// </summary>
        /// <param name="device"></param>
        /// <param name="command"></param>
        /// <param name="data"></param>
        [UsedImplicitly]
        public void SendCommand(SlaveDevice device, byte command, byte[] data = null)
        {
            SendCommandInternal(new Message(device.ID, ID, command){Data = data});
        }

        /// <summary>
        /// Send message on port of master device
        /// </summary>
        /// <param name="message"></param>
        [UsedImplicitly]
        public void SendCommand(Message message)
        {
            _logger.Trace("thread is ready to enter the critical section");
            lock (_locker)
            {
                _logger.Trace("thread entered the critical section");
                SendCommandInternal(message);
                _logger.Trace("thread is ready to exit the critical section");
            }
            Monitor.Exit(_locker);
            _logger.Trace("thread exited the critical section");
        }

        /// <summary>
        /// Receive message from port of master device
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns></returns>
        public Message ReceiveMessage(int timeout = DefaultTimeout)
        {
            Message result;
            _logger.Trace("thread is ready to enter the critical section");
            lock (_locker)
            {
                _logger.Trace("thread entered the critical section");
                result = ReceiveMessageInternal(timeout);
                _logger.Trace("thread is ready to exit the critical section");
            }
            _logger.Trace("thread exited the critical section");
            return result;
        }

        /// <summary>
        /// Executes specified command by performing both operations send and receive which locked at one critical section
        /// </summary>
        /// <param name="device"></param>
        /// <param name="command"></param>
        /// <param name="timeout">Timeout for response (default: MasterDevice.DefaultTimeout)</param>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="ThreadInterruptedException"></exception>
        public Message ExecCommand(SlaveDevice device, byte command, int timeout = DefaultTimeout, byte[] data = null)
        {
            Message result;
            _logger.Trace("thread is ready to enter the critical section _locker");
            lock (_locker)
            {
                _logger.Trace("thread entered the critical section _locker");
                SendCommandInternal(device, command, data);
                _logger.Trace("command is sent, receiving _locker");
                result = ReceiveMessageInternal(timeout);
                _logger.Trace("thread is ready to exit from the critical section _locker");
            }
            _logger.Trace("thread exited from the critical section _locker");
            return result;
        }

        private void SendCommandInternal(SlaveDevice device, byte command, byte[] data = null)
        {
            SendCommandInternal(new Message(device.ID, ID, command){Data = data});
        }

        private void SendCommandInternal(Message message, int timeout = DefaultTimeout)
        {
            _logger.Trace("-------- TX: " + string.Join(" ", message.ToBytes().Select(x => x.ToString("X2"))));
            if (State != MasterDeviceState.Idle)
            {
                _logger.Trace("Master device is busy");
                throw new Exception("Master device is busy");
            }
            try
            {
                State = MasterDeviceState.Sending;
                LastMessage = message;
                var msg = LastMessage.ToBytes();
                lock (_syncRoot4SerialPortActions)
                {
                    if (Port == null || !Port.IsOpen)
                    {
                        _logger.Trace("Master device is not available");
                        throw new Exception("Master device is not available");
                    }
                    Port.DiscardInBuffer();
                    Port.DiscardOutBuffer();
                    Port.WriteTimeout = timeout;
                    _logger.Trace("try write bytes");
                    Port.Write(msg, 0, msg.Length);
                    _logger.Trace("bytes has been wrote");
                }
            }
            catch (Exception ex)
            {
                _logger.TraceException(string.Empty, ex);
            }
            finally
            {   
                State = MasterDeviceState.Idle;
            }
            
        }

        private Message ReceiveMessageInternal(int timeout)
        {
            _logger.Trace("start, timeout: " + timeout);
            if (State != MasterDeviceState.Idle)
            {
                _logger.Debug("Master device is busy");
                throw new Exception("Master device is busy");
            }
            var buffer = new List<byte>();
            var replyBit = false;
            Message result;
            // 2 reasons for break cycle
            // 1) end of message 
            // 2) time is out 
            try
            {
                State = MasterDeviceState.Receiving;
                lock (_syncRoot4SerialPortActions)
                {
                    if (Port == null || !Port.IsOpen)
                    {
                        _logger.Debug("Master device is not available");
                        throw new Exception("Master device is not available");
                    }
                    Port.ReadTimeout = timeout;
                }
                
                while (true)
                {
                    //if (TraceThreadBlocking)
                    //    Debug.WriteLine("{1} thread {0} try to read byte", Task.CurrentId, DateTime.Now);
                    byte b;
                    lock (_syncRoot4SerialPortActions)
                    {
                        if (Port == null || !Port.IsOpen)
                        {
                            _logger.Debug("Master device is not available");
                            throw new Exception("Master device is not available");
                        }
                        b = (byte) Port.ReadByte();
                    }
                    replyBit = true;
                    buffer.Add(b);

                    // chech if message complete
                    if (buffer.Count < CctalkMessage.MinMessageLength ||
                        buffer.Count != CctalkMessage.MinMessageLength + buffer[CctalkMessage.PosDataLen]) continue;

                    _logger.Trace("end message");

                    // check if echo
                    if (!LastMessage.ToBytes().SequenceEqual(buffer))
                    {
                        if (buffer.Sum(x => x)%256 != 0)
                        {
                            _logger.Debug("Invalid checksum");
                            throw new Exception("Invalid checksum");
                        }
                        break;
                    }
                    buffer.Clear();
                }

                _logger.Trace("-------- RX: " + string.Join(" ", buffer.Select(x => x.ToString("X2"))));

                result = new Message(buffer[CctalkMessage.PosDestAddr], buffer[CctalkMessage.PosSourceAddr],
                    buffer[CctalkMessage.PosHeader])
                {
                    Data = buffer.Skip(4).Take(buffer[CctalkMessage.PosDataLen]).ToArray()
                };
            }
            catch (TimeoutException ex)
            {
                if (!replyBit)
                    throw new TimeoutException("Slave device does not respond", ex);

                result = GetDefaultMessage(buffer);
            }
            catch (Exception ex)
            {
                _logger.DebugException(String.Empty, ex);
                result = GetDefaultMessage(buffer);
            }
            finally
            {
                State = MasterDeviceState.Idle;
            }

            LastMessage = result;
            return result;
        }

        private Message GetDefaultMessage(List<byte> buffer)
        {
            // Todo: handle broken message
            _logger.Debug("Message is broken: " +
                            string.Join(" ", buffer.Select(x => x.ToString("x2")).ToArray()));
            var m = new Message(
                buffer.Count >= 1 ? buffer[CctalkMessage.PosDestAddr] : (byte) 0,
                buffer.Count >= 3 ? buffer[CctalkMessage.PosSourceAddr] : (byte) 0,
                buffer.Count >= 4 ? buffer[CctalkMessage.PosHeader] : (byte) 0
                );
            if (buffer.Count > 5)
                m.Data = buffer.Skip(4).Take(buffer[CctalkMessage.PosDataLen]).ToArray();
            return m;
        }
    }

    /// <summary>
    /// Represents message transmitted or received by master device to/from slave device
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Destination address
        /// </summary>
        [UsedImplicitly]
        public byte Dest { set; get; }
        /// <summary>
        /// Data part length of message
        /// </summary>
        [UsedImplicitly]
        public byte DataLen { get { return (byte)(Data != null ? Data.Length : 0); } }
        /// <summary>
        /// Source address
        /// </summary>
        [UsedImplicitly]
        public byte Source { set; get; }
        /// <summary>
        /// Command
        /// </summary>
        [UsedImplicitly]
        public byte Header { set; get; }
        /// <summary>
        /// Data (format not restricted)
        /// </summary>
        [UsedImplicitly]
        public byte[] Data { set; get; }
        /// <summary>
        /// Checksum (for now just base algorythm %256)
        /// </summary>
        [UsedImplicitly]
        public byte CheckSum { get { return (byte)(256 - (Dest + DataLen + Source + Header + (Data != null && Data.Count() > 0 ? Data.Sum(x => x) : 0)) % 256); } }
        /// <summary>
        /// Contains raw data of received message (not used)
        /// </summary>
        [UsedImplicitly]
        public byte[] Bytes { private set; get; }
        /// <summary>
        /// .ctor
        /// </summary>
        public Message() { }
        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="command"></param>
        public Message(byte dest, byte source, byte command)
        {
            Dest = dest;
            Source = source;
            Header = command;
        }
        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="bytes"></param>
        public Message(byte[] bytes)
        {
            Bytes = bytes;
        }
        /// <summary>
        /// Converts message to byte array for transmitting
        /// </summary>
        /// <returns></returns>
        public byte[] ToBytes()
        {
            var res = new[] { Dest, DataLen, Source, Header };
            return (Data != null ? res.Concat(Data).Concat(new[] { CheckSum }) : res.Concat(new[] { CheckSum })).ToArray();
        }
    }
}
