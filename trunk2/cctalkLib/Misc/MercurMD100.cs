using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using dk.CctalkLib.Annotations;
using NLog;

namespace dk.CctalkLib.Misc
{
    /// <summary>
    /// Represents MercurMD100 2 in 1 (Stacker + dispenser) device
    /// </summary>
    public class MercurMD100: SlaveDevice
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private const byte MaxEventLogLength = 5;
        private static readonly IEnumerable<byte> billTypeIDsForAcception = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 21 };
        private static readonly IEnumerable<byte> storeTypeIDsForAcception = new byte[]
                                        {
                                            0, 1, (byte) BillTypeToDispense.Disp_SS1,
                                            (byte) BillTypeToDispense.Disp_SS2,
                                            (byte) BillTypeToDispense.Disp_SS3
                                        };
        private readonly Queue<MD100Event> _eventLog = new Queue<MD100Event>(MaxEventLogLength);
        private PollingEventType _deviceState;
        //private bool _isReseted;
        private bool _newEvent;
        private readonly object _syncRoot4LastEvents = new object();
        private readonly object _syncRoot4MacroOperationExecutingState = new object();

        private string _categoryID;
        private string _productCode;
        private string _buildCode;
        private string _manufacturerID;
        private string _serialNumber;
        private MercurMD100SoftwareRevision _softwareRevision = new MercurMD100SoftwareRevision();
        private ccTalkVersion _commandsVersion;
        private MercurMD100OptionFlags _optionFlags;
        private List<string> _checksums = new List<string>();
        private ScalingFactor _scalingFactor;
        private string _currencyRevision;
        private readonly BillTypeCollection _billType = new BillTypeCollection();
        private OperationMode _operationBillMode;

        #region Fields

        /// <summary>
        /// Checks whether the another operation is executing
        /// </summary>
        [UsedImplicitly]
        public bool IsMacroOperationExecuting { private set; get; }

        /// <summary>
        /// Checks whether the slave is ready to execute the command
        /// </summary>
        [UsedImplicitly]
        public bool IsReady { get { return IsStateReady(DeviceState); } }

        private static bool IsStateReady(PollingEventType pollingEventType)
        {
            // note: documentation describes this condition, in fact it may be otherwise
            return pollingEventType >= PollingEventType._CIDLE;
        }

        /// <summary>
        /// CategoryID
        /// </summary>
        [UsedImplicitly]
        public string CategoryID
        {
            set { _categoryID = value; RaisePropertyUpdatedEvent("CategoryID"); }
            get { return _categoryID; }
        }

        /// <summary>
        /// ProductCode
        /// </summary>
        [UsedImplicitly]
        public string ProductCode
        {
            set { _productCode = value; RaisePropertyUpdatedEvent("ProductCode"); }
            get { return _productCode; }
        }

        /// <summary>
        /// BuildCode
        /// </summary>
        [UsedImplicitly]
        public string BuildCode
        {
            set { _buildCode = value; RaisePropertyUpdatedEvent("BuildCode"); }
            get { return _buildCode; }
        }

        /// <summary>
        /// ManufacturerID
        /// </summary>
        [UsedImplicitly]
        public string ManufacturerID
        {
            set { _manufacturerID = value; RaisePropertyUpdatedEvent("ManufacturerID"); }
            get { return _manufacturerID; }
        }

        /// <summary>
        /// SerialNumber
        /// </summary>
        [UsedImplicitly]
        public string SerialNumber
        {
            set { _serialNumber = value; RaisePropertyUpdatedEvent("SerialNumber"); }
            get { return _serialNumber; }
        }

        /// <summary>
        /// SoftwareRevision
        /// </summary>
        [UsedImplicitly]
        public MercurMD100SoftwareRevision SoftwareRevision
        {
            set { _softwareRevision = value; RaisePropertyUpdatedEvent("SoftwareRevision"); }
            get { return _softwareRevision; }
        }

        /// <summary>
        /// ccTalk CommandsVersion
        /// </summary>
        [UsedImplicitly]
        public ccTalkVersion CommandsVersion
        {
            set { _commandsVersion = value; RaisePropertyUpdatedEvent("CommandsVersion"); }
            get { return _commandsVersion; }
        }

        /// <summary>
        /// Current device state
        /// </summary>
        [UsedImplicitly]
        public PollingEventType DeviceState
        {
            get { return _deviceState; }
            set { 

                _deviceState = value;
                
                if (IsStateReady(_deviceState))
                    _readyEvent.Set();
                else
                    _readyEvent.Reset();

                RaiseDeviceStateChangedEvent(value); 
            }
        }

        /// <summary>
        /// OptionFlags
        /// </summary>
        [UsedImplicitly]
        public MercurMD100OptionFlags OptionFlags
        {
            set { _optionFlags = value; RaisePropertyUpdatedEvent("OptionFlags"); }
            get { return _optionFlags; }
        }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public List<string> Checksums
        {
            set { _checksums = value; RaisePropertyUpdatedEvent("Checksums"); }
            get { return _checksums; }
        }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public ScalingFactor ScalingFactor
        {
            set { _scalingFactor = value; RaisePropertyUpdatedEvent("ScalingFactor"); }
            get { return _scalingFactor; }
        }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public string CurrencyRevision
        {
            set { _currencyRevision = value; RaisePropertyUpdatedEvent("CurrencyRevision"); }
            get { return _currencyRevision; }
        }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public BillTypeCollection BillTypes { get { return _billType; } }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public OperationMode OperationBillMode
        {
            set { _operationBillMode = value; RaisePropertyUpdatedEvent("OperationBillMode"); }
            get { return _operationBillMode; }
        }

        /// <summary>
        /// Indicates whether a flag for some BillType
        /// </summary>
        [UsedImplicitly]
        public bool IsAcceptingState
        {
            get
            {
                return BillTypes[1].IsSet || BillTypes[2].IsSet ||
                       BillTypes[3].IsSet || BillTypes[4].IsSet ||
                       BillTypes[5].IsSet || BillTypes[6].IsSet;
            }
        }

        #endregion

        #region events

        #region DeviceStateChanged
        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public event DeviceStateChangedEventHandler DeviceStateChanged;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        public delegate void DeviceStateChangedEventHandler(object sender, DeviceStateChangedEventArgs eventArgs);

        private void RaiseDeviceStateChangedEvent(PollingEventType eventType)
        {
            if (DeviceStateChanged == null) return;
            DeviceStateChanged(this, new DeviceStateChangedEventArgs { EventType = eventType });
            //if (macroOperations.Count <= 0) return;
            _logger.Debug("DeviceStateChanged: " + eventType);

            if (IsReady)
            {
                BreakAcceptingProc();
            }

            switch (eventType)
            {
                case PollingEventType._CRESET:
                    break;

                case PollingEventType._CINIT:
                    BreakAcceptingProc();
                    break;

                case PollingEventType._CPAYMODE:
                    _acceptingProcIsPayModeOccured = true;
                    break;

                case PollingEventType._CBUSY:
                    _acceptingProcIsBusyOccured = true;
                    break;

                case PollingEventType.BillAccepted:
                    if (!_acceptingProcIsPayModeOccured && _acceptingProcIsBusyOccured)
                    {
                        //_acceptingProcIsBillAcceptedOccured = true;
                        byte acceptingprocBillType;
                        lock (_syncRoot4LastEvents)
                            acceptingprocBillType = _eventLog.Last().EventData[1];
                        RaiseBillAcceptedEvent(acceptingprocBillType);
                    }
                    else
                        BreakAcceptingProc();
                    break;

                case PollingEventType._CIDLE:
                    break;

                case PollingEventType._CLOCKED:
                    break;
            }
        }

        /// <summary>
        /// handler for the situation <see cref="PollingEventType.BillAccepted"/>
        /// </summary>
        private void BreakAcceptingProc()
        {
            _acceptingProcIsPayModeOccured = false;
            _acceptingProcIsBusyOccured = false;
        }

        /// <summary> if true - dispense </summary>
        private bool _acceptingProcIsPayModeOccured;
        private bool _acceptingProcIsBusyOccured;

        /// <summary>
        /// 
        /// </summary>
        public class DeviceStateChangedEventArgs
        {
            /// <summary>
            /// Contais new device state
            /// </summary>
            [UsedImplicitly]
            public PollingEventType EventType { set; get; }
        }

        #endregion

        #region PollResponse

        /// <summary>
        /// Event handler for poll response
        /// </summary>
        [UsedImplicitly]
        public event PollResponseEventHandler PollResponse;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        public delegate void PollResponseEventHandler(object sender, PollResponseEventArgs eventArgs);

        private void RaisePollResponseEvent(List<MD100Event> lastEvents)
        {
            _logger.Trace("start");
            lastEvents.Reverse();
            if (lastEvents.First().EventType == PollingEventType.NoEvents)
            {
                lock (_syncRoot4LastEvents)
                    _eventLog.Clear();
                _logger.Trace("no events -> return");
                return;
            }

            var newEventCount = 0;
            if (lastEvents.Count > _eventLog.Count) 
                newEventCount = lastEvents.Count;
            else
            {
                var log = _eventLog.Skip(_eventLog.Count - lastEvents.Count).ToArray();

                while (newEventCount <= MaxEventLogLength)
                {
                    var b = log.Skip(newEventCount).Take(MaxEventLogLength - newEventCount).Select(x => x.EventNumber)
                        .SequenceEqual(lastEvents.Take(MaxEventLogLength - newEventCount).Select(x => x.EventNumber));
                    if (b) break;
                    newEventCount++;
                }
            }

            _newEvent = newEventCount > 0;
            if (!_newEvent)
            {
                _logger.Trace("no new events -> return");
                return;
            }

            var eventsForInsert = (_eventLog.Count <= 0 ? lastEvents : lastEvents.Skip(MaxEventLogLength-newEventCount)).ToArray();
            foreach (var @event in eventsForInsert)
            {
                lock (_syncRoot4LastEvents)
                {
                    if (_eventLog.Count >= MaxEventLogLength) 
                        _eventLog.Dequeue();
                    _eventLog.Enqueue(@event);
                }
                _logger.Debug("New event: " + @event.EventNumber + " " + @event.EventType);
                DeviceState = _eventLog.Last().EventType;
            }

            if (PollResponse == null) return;
            PollResponse(this, new PollResponseEventArgs { LastEvents = lastEvents });
        }

        /// <summary> </summary>
        public class PollResponseEventArgs
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public List<MD100Event> LastEvents { set; get; }
        }

        #endregion

        #region PropertyUpdated

        /// <summary>
        /// Occurs when one of the properties has been updated
        /// </summary>
        [UsedImplicitly]
        public event PropertyUpdatedEventHandler PropertyUpdated;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        public delegate void PropertyUpdatedEventHandler(object sender, PropertyUpdatedEventArgs eventArgs);

        private void RaisePropertyUpdatedEvent(string propertyName)
        {
            if (PropertyUpdated == null) return;
            PropertyUpdated(this, new PropertyUpdatedEventArgs{PropertyName = propertyName});
        }

        /// <summary>
        /// 
        /// </summary>
        public class PropertyUpdatedEventArgs
        {
            /// <summary>
            /// Name of property which was updated
            /// </summary>
            [UsedImplicitly]
            public string PropertyName { set; get; }
        }

        #endregion

        #region BillAccepted
        /// <summary>
        /// Occurs when bill has been accepted
        /// </summary>
        [UsedImplicitly]
        public event BillAcceptedEventHandler BillAccepted;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        public delegate void BillAcceptedEventHandler(object sender, BillAcceptedEventArgs eventArgs);

        private void RaiseBillAcceptedEvent(int billTypeID)
        {
            if (BillAccepted == null) return;
            if (billTypeID < 1 || billTypeID > 21) return;
            BillAccepted(this, new BillAcceptedEventArgs { BillTypeID = billTypeID, Nominal = BillTypes[billTypeID].ValueCode });
        }

        /// <summary> </summary>
        public class BillAcceptedEventArgs
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public int BillTypeID { set; get; }

            /// <summary> </summary>
            [UsedImplicitly]
            public int Nominal { set; get; }
        }

        #endregion

        #endregion

        #region enums 

        /// <summary>
        /// Represents macro operation for device
        /// </summary>
        public enum MacroOperationType
        {
            /// <summary> </summary>
            [UsedImplicitly]
            Reset,
            /// <summary> </summary>
            [UsedImplicitly]
            BillInsertOnInactiveEscrow,
        }

        /// <summary> </summary>
        public class MacroOperation
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public MacroOperationType Type;
            /// <summary> </summary>
            [UsedImplicitly]
            public List<bool> Checkpoints = new List<bool>();
        }

        /// <summary>
        /// option flags of bill validator
        /// </summary>
        [Flags]
        public enum MercurMD100OptionFlags
        {
            /// <summary>  </summary>
            [UsedImplicitly]
            Stacker = 1,
            /// <summary>  </summary>
            [UsedImplicitly]
            Escrow = 2,
            /// <summary>  </summary>
            [UsedImplicitly]
            FillInGame = 4,
            /// <summary>  </summary>
            [UsedImplicitly]
            IgnoreCboxFull = 8,
            /// <summary>  </summary>
            [UsedImplicitly]
            BillReverseCheck = 64
        }

        /// <summary>
        /// AFD-specific status events 
        /// </summary>
        public enum PollingEventType
        {
            /// <summary>
            /// EventsList is empty
            /// </summary>
            [UsedImplicitly]
            NoEvents,
            /// <summary>
            /// <para>Describes multiple values for situation of bill acception</para>
            /// <para>all events of bill-insertion now give information about the billtype and where the bill stores billtypes: </para>
            /// <para>(high byte) could be: 1...16 possible types of acceptor, 17 = unknown bill and 21 = ticket the place of store </para>
            /// <para>(low byte) could be : 00 cashbox, 01 hold in escrow position, 12 store to SS1 13 store to SS2, 14 store to SS3</para>
            /// </summary>
            [UsedImplicitly]
            BillAccepted,
            /// <summary> reset </summary>
            [UsedImplicitly]
            _CRESET = 0x0020,
            /// <summary> connection to master </summary>
            [UsedImplicitly]
            _CONNECT = 0x0021,
            /// <summary> init of MD-100 after Reset, this can be done in 20sec </summary>
            [UsedImplicitly]
            _CINIT = 0x0022,
            /// <summary> married (only for encryption version) </summary>
            [UsedImplicitly]
            _CMARRY = 0x0023,
            /// <summary> update AFD / MD100 active </summary>
            [UsedImplicitly]
            _CUPDATE = 0x0024,
            /// <summary> configuration of SS1..SS3 Dispenser </summary>
            [UsedImplicitly]
            _CCONFIG = 0x0025,
            /// <summary> busy a bill is in transport </summary>
            [UsedImplicitly]
            _CBUSY = 0x0026,
            /// <summary> idle ready for work </summary>
            [UsedImplicitly]
            _CIDLE = 0x0027,
            /// <summary> acceptor global locked </summary>
            [UsedImplicitly]
            _CLOCKED = 0x0028,
            /// <summary> acceptor global free </summary>
            [UsedImplicitly]
            _CFREE = 0x0029,
            /// <summary> only after a error occurs, (may be bill jammed), the bill moves to cashbox </summary>
            [UsedImplicitly]
            _CMOVCBOX = 0x002A,
            /// <summary> after payout, the bill stays on the acceptor frontend, user should remove it </summary>
            [UsedImplicitly]
            _CMOVBILL = 0x002B,
            /// <summary> change Op-Mode to Sys_S_Fill </summary>
            [UsedImplicitly]
            _CHANGETOFILL = 0x002C,
            /// <summary> change Op-Mode to Sys_S_Unload </summary>
            [UsedImplicitly]
            _CHANGETOUNLOAD = 0x002D,
            /// <summary> change Op-Mode to Sys_Game </summary>
            [UsedImplicitly]
            _CHANGETOGAME = 0x002E,
            /// <summary> modify acceptor billtypes </summary>
            [UsedImplicitly]
            _CMODTYPES = 0x002F,
            /// <summary> AFD try to start a pay out of one bill </summary>
            [UsedImplicitly]
            _CPAYMODE = 0x0030,
            /// <summary> specific error events 'e' + error number </summary>
            [UsedImplicitly]
            _CERROR = 0x6500
        }

        /// <summary>
        /// ccTalk commands set expension specific for md100
        /// </summary>
        [UsedImplicitly]
        public enum MercurMD100Commands
        {
            /// <summary>  </summary>
            RequestFillsize = 94,
            /// <summary>  </summary>
            SetDateTime = 95,
            /// <summary>  </summary>
            MasterDispense = 97
        }

        /// <summary>
        /// Op - Modes: for Sys_Game (default) - Sys_Unload = 0 & Sys_Fill = 0, not allowed both setting Sys_Game & Sys_Unload
        /// </summary>
        [Flags]
        public enum ModeControlMask
        {
            /// <summary> all inserted bills moves to cashbox </summary>
            [UsedImplicitly]
            StackerSupported = 1 << 0,//,    
            /// <summary> all inserted bills holds on escrow position  </summary>
            [UsedImplicitly]
            EscrowSupported = 1 << 1, //, 
            /// <summary>  </summary>
            [UsedImplicitly]
            Refill_in_GameMode = 1 << 2,
            /// <summary>  </summary>
            [UsedImplicitly]
            Ignore_cbox_full = 1 << 3,                     
            /// <summary> move_bills from dispenser to cashbox  </summary>
            [UsedImplicitly]
            Sys_Unload = 1 << 4, //
            /// <summary> only dispenseable billtypes enabled, pay out disabled  </summary>
            [UsedImplicitly]
            Sys_Fill = 1 << 5, //, 
            /// <summary>  </summary>
            [UsedImplicitly]
            BillReverseCheckActive = 1 << 6
        }

        /// <summary>
        /// 
        /// </summary>
        public enum OperationMode
        {
            /// <summary>  </summary>
            [UsedImplicitly]
            Sys_Game = 0,
            /// <summary>  </summary>
            [UsedImplicitly]
            Sys_Unload = ModeControlMask.Sys_Unload,
            /// <summary>  </summary>
            [UsedImplicitly]
            Sys_Fill = ModeControlMask.Sys_Fill
        }

        /// <summary>
        /// Dispenser has 3 slots, each slot corresponds to its value
        /// </summary>
        public enum BillTypeToDispense
        {
            /// <summary> For init purposes </summary>
            Test_Dispense = 0,
            /// <summary> First slot </summary>
            Disp_SS1 = 0x12,
            /// <summary> Second slot </summary>
            Disp_SS2 = 0x13,
            /// <summary> Third slot </summary>
            Disp_SS3 = 0x14,
        }

        /// <summary>
        /// note: ????
        /// </summary>
        public enum PayoutStrategie
        {
            /// <summary> </summary>
            [UsedImplicitly]
            BIGGEST_BILL_FIRST = 0x00,
            /// <summary> </summary>
            [UsedImplicitly]
            BIGGEST_STOCK_FIRST = 0x10
        }

        /// <summary>
        /// note: it is not clear what it is, but on the documentation, this value should always be equal to 2 (Real), and after rebooting the device must be set to 0 (Test)
        /// </summary>
        public enum Paymode
        {
            /// <summary> </summary>
            [UsedImplicitly]
            Test = 0,
            /// <summary> </summary>
            [UsedImplicitly]
            Real = 2
        }

        #endregion

        #region classes

        /// <summary>
        /// Contains software revision strings separated according to MD100 specs
        /// </summary>
        public class MercurMD100SoftwareRevision
        {
            /// <summary>  </summary>
            [UsedImplicitly]
            public string SW_Revision { set; get; }

            /// <summary>  </summary>
            [UsedImplicitly]
            public string AcceptorEncryption { set; get; }

            /// <summary>  </summary>
            [UsedImplicitly]
            public string AcceptorHead { set; get; }

            /// <summary>  </summary>
            [UsedImplicitly]
            public string Dispenser { set; get; }
        }

        /// <summary>
        /// 
        /// </summary>
        public class MD100Event
        {
            /// <summary>
            /// Contains device state
            /// </summary>
            public PollingEventType EventType
            {
                get
                {
                    if (EventNumber == 0 && EventData[0] == 0 && EventData[1] == 0) 
                        return PollingEventType.NoEvents;
                    return (billTypeIDsForAcception.Contains(EventData[1]) &&
                            storeTypeIDsForAcception.Contains(EventData[0]))
                               ? PollingEventType.BillAccepted
                               : (PollingEventType) EventData[0];
                }
            }

            /// <summary> Contains eventtype and additional data </summary>
            public byte[] EventData { set; get; }

            /// <summary> Contains number of event within MercurMD100 history list </summary>
            public byte EventNumber { set; get; }

            /// <summary> IsUsedAfterWaiting </summary>
            public bool IsUsedAfterWaiting { set; get; }

            /// <summary> .ctor </summary>
            public MD100Event()
            {
                EventData = new byte[2];
            }

            public override string ToString()
            {
                return EventNumber + " " + EventType;
            }
        }
        
        /// <summary>
        /// Represents bill types collection of this device
        /// </summary>
        public class BillTypeCollection
        {
            private readonly BillType[] billTypes = new BillType[21];
            /// <summary>
            /// Get or set bill type with specified ID
            /// </summary>
            /// <param name="i">Bill type ID</param>
            /// <returns></returns>
            public BillType this[int i]
            {
                get { return billTypes[i - 1]; }
                set { billTypes[i - 1] = value; }
            }

            /// <summary>.ctor </summary>
            public BillTypeCollection()
            {
                for (var i = 0; i < billTypes.Length; i++)
                {
                    billTypes[i] = new BillType();
                }
            }
        }

        /// <summary>
        /// Specifies the type of payment. in fact this type of used banknotes
        /// </summary>
        public class BillType
        {
            /// <summary> Identificator of bill type 1..21 </summary>
            [UsedImplicitly]
            public byte ID { set; get; }

            /// <summary> Specified if this bill type is used </summary>
            [UsedImplicitly]
            public bool IsSet { set; get; }

            /// <summary> Country of used banknotes </summary>
            [UsedImplicitly]
            public string Country { set; get; }

            /// <summary> Denomination of the banknote </summary>
            [UsedImplicitly]
            public int ValueCode { set; get; }

            /// <summary> Bill count within cashbox of this type </summary>
            [UsedImplicitly]
            public int Fillsize { set; get; }

            /// <summary> max count of bill in dispenser </summary>
            [UsedImplicitly]
            public int MaxFill { set; get; }

            /// <summary> ??? </summary>
            [UsedImplicitly] public string IssueCode;

            /// <summary>  '0' = not available, '1' = locked, '2' = free  </summary>
            [UsedImplicitly] public string State;
        }

        /// <summary>
        /// Contains info about result of dispense operation
        /// </summary>
        public class DispenseResult
        {
            /// <summary>
            /// Actually it is error code
            /// </summary>
            [UsedImplicitly]
            public byte PayoutValue { set; get; }

            /// <summary>
            /// Provides an indication of the errors presence, it's the same to check if PayoutValue is equal to 0
            /// </summary>
            [UsedImplicitly]
            public bool IsOK
            {
                get { return PayoutValue == 0; }
            }

            /// <summary>
            /// Contains default decription of error message
            /// </summary>
            [UsedImplicitly]
            public string ErrorDescription
            {
                get
                {
                    switch (PayoutValue)
                    {
                        case 0:
                            return "No errors";
                        case 0xA7:
                            return "FALSE BILLTYPE OR NONE FILLED";
                        case 0xA6:
                            return "CCT PAY OUT DENIED, OLD PAY OUT IN PROCESS";
                        case 0xA5:
                            return "A5 CCT PAY OUT DENIED, VALIDATION FAIL (active encryption)";
                        default:
                            return "Unknown error";
                    }
                }
            }

            /// <summary>
            /// In case of an error due to another operation, the field contains old number of bills to dispense
            /// </summary>
            [UsedImplicitly]
            public byte OldNumberOfBillsToDispense { set; get; }

            /// <summary>
            /// In case of an error due to another operation, the field contains old bill type to dispense
            /// </summary>
            [UsedImplicitly]
            public BillTypeToDispense OldBilltypeToDispense { set; get; }
        }

        #endregion

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="id"></param>
        /// <param name="masterDevice"></param>
        public MercurMD100(byte id, MasterDevice masterDevice): base(id, masterDevice)
        {
            
        }

        /// <summary>
        /// 
        /// </summary>
        public override void TerminateDevice()
        {
            CancelMacroOperation();
            PollingStop();
        }

        /// <summary> </summary>
        [UsedImplicitly]
        public bool IsPollingActive { get; private set; }

        private Task pollingTask;
        private CancellationTokenSource pollingCancellationTokenSource;

        /// <summary>
        /// Starts a poll of device for upcoming events
        /// </summary>
        [UsedImplicitly]
        public void PollingStart()
        {
            if (IsPollingActive)
                return;
            if (pollingTask != null || pollingCancellationTokenSource != null)
                PollingStop();
            pollingCancellationTokenSource = new CancellationTokenSource();
            var ct = pollingCancellationTokenSource.Token;
            pollingTask = new Task(() =>
            {
                IsPollingActive = true;
                var isDeviceActive = false;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (MasterDevice.Port == null || !MasterDevice.Port.IsOpen)
                            continue;
                        var lastEvents = GetLastEvents();
                        RaisePollResponseEvent(lastEvents);
                        if (isDeviceActive == false)
                        {
                            isDeviceActive = true;
                            _logger.Debug("Device work");
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        if (isDeviceActive)
                        {
                            _logger.DebugException("Device is not available", ex);
                            isDeviceActive = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException(string.Empty, ex);
                    }
                    Thread.Sleep(200);
                }
                IsPollingActive = false;
            }, pollingCancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            pollingTask.Start();
        }

        /// <summary>
        /// Stops device polling
        /// </summary>
        [UsedImplicitly]
        public void PollingStop()
        {
            try
            {
                if (pollingCancellationTokenSource != null) 
                    pollingCancellationTokenSource.Cancel();
                if (pollingTask != null) 
                    pollingTask.Wait();
            }
            catch (AggregateException aex)
            {
                var cex = aex.InnerException as TaskCanceledException;
                if (!(cex != null && cex.CancellationToken.IsCancellationRequested))
                    throw;
            }
            pollingTask = null;
            if (pollingCancellationTokenSource != null) 
                pollingCancellationTokenSource.Dispose();
            pollingCancellationTokenSource = null;
        }

        #region MD100 commands

        private List<MD100Event> GetLastEvents()
        {
            //Debug.WriteLine("{1} thread {0} GetLastEvents start", Task.CurrentId, DateTime.Now);
            var res = ReadBufferedBillEvents();
            if (res.Counter == 0)
                return new List<MD100Event> {new MD100Event {EventNumber = 0}};
            var evs = new List<MD100Event>();
            for (var i = 0; i < res.Events.Length; i++)
            {
                if (res.Events[i].ErrorOrRouteCode == 0 && res.Events[i].CoinCode == 0) continue;
                evs.Add(new MD100Event
                            {
                                EventData = new[] {res.Events[i].ErrorOrRouteCode, res.Events[i].CoinCode},
                                EventNumber = (byte) (res.Counter - i)
                            });
            }
            //Debug.WriteLine("{1} thread {0} GetLastEvents finish", Task.CurrentId, DateTime.Now);
            return evs;
        } 


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private new string RequestSerialNumber()
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestSerialNumber);
            return res.Data.Reverse().Aggregate("", (a, b) => a + b);
        }

        /// <summary>
        /// Returns software revision strings transformed into SoftwareRevision class
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public new MercurMD100SoftwareRevision RequestSoftwareRevision()
        {
            var res = base.RequestSoftwareRevision();
            var res2 = new MercurMD100SoftwareRevision();
            res2.SW_Revision = string.IsNullOrEmpty(res) ? "" : res.Substring(0, 32);
            res2.AcceptorEncryption = string.IsNullOrEmpty(res) ? "" : res.Substring(32, 32);
            res2.AcceptorHead = string.IsNullOrEmpty(res) ? "" : res.Substring(64, 32);
            res2.Dispenser = string.IsNullOrEmpty(res) ? "" : res.Substring(96, 32);
            for (var i = 0; i < 10; i++)
            {
                res2.Dispenser = res2.Dispenser.Replace(((char) i).ToString(CultureInfo.InvariantCulture), i.ToString("<00>"));
            }
            return res2;
        }

        /// <summary>
        /// Slave get back the option flags of bill validator
        /// </summary>
        [UsedImplicitly]
        public new MercurMD100OptionFlags RequestOptionFlags()
        {
            return (MercurMD100OptionFlags)base.RequestOptionFlags();
        }

        /// <summary>
        ///  Slave response his CRC, Achtung: the crc is calculated on a adp-specific algorythm and not compatible with the calculation of crc on the cctalk spec.
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public List<string> GetChecksum()
        {
            var res1 = CalculateROMChecksum().Reverse().ToList();
            var res2 = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var f = new byte[2];
                f[0] = res1.Count > i * 2 ? res1[i * 2] : (byte)0;
                f[1] = res1.Count > i * 2 + 1 ? res1[i * 2 + 1] : (byte)0;
                res2.Add(f.Aggregate("", (a, b) => a + b.ToString("X2")));
            }
            return res2;
        }

        /// <summary>
        /// Set system date time to device
        /// </summary>
        /// <param name="value"></param>
        [UsedImplicitly]
        public void SetDateTime(DateTime value)
        {
            var formattedValue = value.ToString("ddMMyyhhmmss");
            ExecCommand((byte)MercurMD100Commands.SetDateTime, MasterDevice.DefaultTimeout, Encoding.ASCII.GetBytes(formattedValue));
        }

        /// <summary>
        /// Slave response his billtype 
        /// </summary>
        /// <param name="billTypeId">number of billtype (1..21) (1..16 = Acceptor, 17 = unknown bills , 18..20 Dispenser, 21 ticket)</param>
        /// <returns></returns>
        [UsedImplicitly]
        public BillType RequestBillType(int billTypeId)
        {
            var res = RequestBillId((byte)billTypeId);
            var bt = new BillType();
            bt.ID = (byte)billTypeId;
            bt.Country = string.IsNullOrEmpty(res) ? "" : res.Substring(0, 2);
            bt.ValueCode = string.IsNullOrEmpty(res) ? 0 : Convert.ToInt32(res.Substring(2, 4));
            bt.IssueCode = string.IsNullOrEmpty(res) ? "" : res.Substring(6, 1);
            bt.Fillsize = string.IsNullOrEmpty(res) ? 0 : Convert.ToInt32(res.Substring(7, 4));
            bt.MaxFill = string.IsNullOrEmpty(res) ? 0 : Convert.ToInt32(res.Substring(11, 4));
            bt.State = string.IsNullOrEmpty(res) ? "" : res.Substring(14, 1);
            return bt;
        }

        /// <summary>
        /// requests inhibit status of bill types and sets them
        /// </summary>
        /// <returns>bit mask of inhibit status</returns>
        [UsedImplicitly]
        public new int RequestInhibitStatus()
        {
            var res1 = base.RequestInhibitStatus();
            var res2 = ConvertInhibitBytesToBitMask(res1);
            SetBillTypesSelection(res2);
            return res2;
        }

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly]
        public void ModifyInhibitStatus(int mask)
        {
            var bytes = ConvertBitMaskToInhibitBytes(mask);
            ModifyInhibitStatus(bytes);
            SetBillTypesSelection(mask);
        }

        private int ConvertInhibitBytesToBitMask(byte[] bytes)
        {
            return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        }

        private byte[] ConvertBitMaskToInhibitBytes(int maskBits)
        {
            var bytes = new byte[3];
            bytes[0] = (byte)(maskBits & 0xff);
            bytes[1] = (byte)((maskBits >> 8) & 0xff);
            bytes[2] = (byte)((maskBits >> 16) & 0xff);
            return bytes;
        }

        private void SetBillTypesSelection(int bitMask)
        {
            for (var i = 0; i < 21; i++)
            {
                BillTypes[i+1].IsSet = ((bitMask >> i) & 1) == 1;
            }
            RaisePropertyUpdatedEvent("BillTypes");
        }

        /// <summary>
        /// Sets new operation mode for device
        /// </summary>
        /// <param name="mask"></param>
        [UsedImplicitly]
        public void SetOperationBillMode(ModeControlMask mask)
        {
            if (!IsReady)
                throw new Exception("operation is not allowed while DeviceState < _CIDLE");
            ModifyBillOperatingMode((byte)mask);
        }

        /// <summary>
        /// Sets new operation mode for device (simplified version)
        /// </summary>
        /// <param name="mask"></param>
        [UsedImplicitly]
        public void SetOperationBillMode(OperationMode mask)
        {
            if (!IsReady)
                throw new Exception("operation is not allowed while DeviceState < _CIDLE");
            ModifyBillOperatingMode((byte)mask);
        }

        /// <summary>
        /// Reads current operation mode of device
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public OperationMode GetOperationMode()
        {
            var res = (ModeControlMask)RequestBillOperatingMode();
            OperationBillMode = (OperationMode) ((res & ModeControlMask.Sys_Fill) | (res & ModeControlMask.Sys_Unload));
            return OperationBillMode;
        }

        /// <summary>
        /// <para>Dispense a Bill</para>
        /// <para>before send a dispense command, the master should do follow: </para>
        /// <para>1   request the actual fillsizes of billcylinder </para>
        /// <para>2   make sure the MD100 is in the right Op_Mode (GAME or BILL_MOVE) </para>
        /// <para>3   When encryption is active its essential !! Send GETTAN command before DISPENSE command,  nothing else !!   </para>
        /// </summary>
        /// <param name="countToDispense">number of bills to dispense, should be always 1, except test value is equal to 0</param>
        /// <param name="billTypeToDispense"></param>
        /// <param name="paymode">paymode should be always 0x02, except test value is equal to 0</param>
        /// <param name="payoutStrategie"></param>
        /// <param name="TAN">pay out validation code TAN when encryption active, otherwhise 0</param>
        /// <returns></returns>
        [UsedImplicitly]
        public DispenseResult MasterDispense(byte countToDispense, BillTypeToDispense billTypeToDispense, Paymode paymode, PayoutStrategie payoutStrategie, byte TAN)
        {
            _logger.Debug("start");
            var data = new byte[6];
            data[0] = countToDispense; // 
            data[1] = (byte) billTypeToDispense;
            data[2] = (byte) paymode; // paymode should be always 0x02 
            data[3] = (byte)payoutStrategie;         
            data[4] = TAN;
            data[5] = TAN;   
            var res = ExecCommand((byte) MercurMD100Commands.MasterDispense, MasterDevice.DefaultTimeout, data);
            var dispenseResult = new DispenseResult();
            if (res.Data != null && res.Data.Length > 0)
            {
                dispenseResult.PayoutValue = res.Data[0];
                if (res.Data.Length >= 3)
                {
                    dispenseResult.OldNumberOfBillsToDispense = res.Data[1];
                    dispenseResult.OldBilltypeToDispense = (BillTypeToDispense)res.Data[2];
                }
            }
            _logger.Debug("finish");
            return dispenseResult;
        }

        /// <summary>
        ///  Slave give back the fillsizes of all billtypes in system as a 16 bit hexvalue
        /// </summary>
        /// <returns>fillsize of any bill in cashbox could be more than 255, so we use a 16bit value. It's list of Int32 with offset -1 (zero based array) </returns>
        [UsedImplicitly]
        public List<int> ReadFillsize()
        {
            var res = ExecCommand((byte) MercurMD100Commands.RequestFillsize);
            var fillsizes = new List<int>();
            for (var i = 0; i < 21; i++)
            {
                fillsizes.Add((res.Data[i * 2] << 8) + res.Data[i * 2 + 1]);
            }
            SetBillTypeFillsizes(fillsizes);
            return fillsizes;
        }

        private void SetBillTypeFillsizes(IList<int> fillsizes)
        {
            for (var i = 1; i <= 21; i++)
            {
                BillTypes[i].Fillsize = fillsizes[i - 1];
                //var bt = BillTypes.FirstOrDefault(x => x.BillTypeID == i + 1);
                //if (bt == null) continue;
                //bt.FillSize = fillsizes[i];
            }
            RaisePropertyUpdatedEvent("BillTypes");
        }

        /// <summary>
        /// Performs set OperationMode and checks this mode
        /// </summary>
        /// <param name="mode"></param>
        [UsedImplicitly]
        public void ChangeOperationMode(OperationMode mode)
        {
            if (!IsReady)
            {
                throw new Exception("device is not ready");
            }
            SetOperationBillMode(mode);
            GetOperationMode();
        }

        /// <summary>
        /// <para>Modify parameters of bil type</para>
        /// <para>only the billtypes of dispenser are modifyable when  </para>
        /// <para>1.  dispenser is idle </para>
        /// <para>2.  SysMode = Sys_S_Leer (Unload) </para>
        /// <para>3.  count of bills = 0 </para>
        /// <para>4.  Billstate = '2' </para>
        /// </summary>
        /// <param name="billType"></param>
        /// <param name="country">Standard 2 letter country code</param>
        /// <param name="codeValue">VVVV - 4 chars which represents bill value in terms of the country scaling factor </param>
        /// <param name="maxFillsize"></param>
        private void ModifyBillType(BillTypeToDispense billType, string country, string codeValue, int maxFillsize)
        {
            if (country.Length != 2) 
                throw new ArgumentException("Wrong length", "country");
            if (codeValue.Length != 4)
                throw new ArgumentException("Wrong length", "codeValue");
            var str = country + codeValue + "0" + "0000" + maxFillsize.ToString("0000") + "2";
            ModifyBillId((byte)billType, str);
        }

        #endregion

        #region Macro operations base

        /// <summary>
        /// 
        /// </summary>
        public class OperationResult
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public bool HasErrors { set; get; }

            /// <summary> Описание результата </summary>
            [UsedImplicitly]
            public string Description { set; get; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class OperationResult<T>: OperationResult
        {
            /// <summary> Значение результата </summary>
            [UsedImplicitly]
            public T Value { set; get; }
        }

        private CancellationTokenSource _cancellationTokenSource;

        private Task<OperationResult<T>> ExecMacroOperation<T>(Func<CancellationToken, T> operationCode, int timeout = Timeout.Infinite)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var task = new Task<OperationResult<T>>(() =>
            {
                Timer timer = null;
                var res = new OperationResult<T> {HasErrors = true};
                try
                {
                    // проверяем не выполняется ли другая операция
                    lock (_syncRoot4MacroOperationExecutingState)
                    {
                        if (IsMacroOperationExecuting)
                            throw new Exception("Another operation is executing");
                        IsMacroOperationExecuting = true;
                        // копируем источник отмены для возможности отмены задания из вне
                        _cancellationTokenSource = cancellationTokenSource;
                    }

                    // если задан таймаут, мастерим на него таймер
                    if (timeout > Timeout.Infinite)
                        RunTaskTimer(ref timer, timeout, cancellationTokenSource);
                    
                    // и наконец выполняем сие действо, конечно же с проверкой его наличия
                    if (operationCode == null)
                        throw new Exception("Operation code is null");
                    res.Value = operationCode(cancellationTokenSource.Token);
                    res.HasErrors = false;
                }
                catch (Exception ex)
                {
                    res.Description = ex.Message;
                    _logger.ErrorException(ex.Message, ex);
                }
                finally
                {
                    lock (_syncRoot4StopTaskTimer)
                    {
                        StopTaskTimer(ref timer);
                        IsMacroOperationExecuting = false;
                        cancellationTokenSource.Dispose();
                        // зануляем глобальный token source
                        _cancellationTokenSource = null;
                    }
                }

                return res;
            },cancellationTokenSource.Token);
            
            return task;
        }

        private readonly object _syncRoot4StopTaskTimer = new object();

        private void RunTaskTimer(ref Timer timer, int timeout, CancellationTokenSource tokenSource)
        {
            StopTaskTimer(ref timer);
            timer = new Timer(state =>
            {
                lock (_syncRoot4StopTaskTimer)
                {
                    try
                    {
                        if (_cancellationTokenSource == null)
                        {
                            _logger.Info("operation timeout: task already stopped");
                            return;
                        }

                        _logger.Info("operation timeout -> request for cancelation");
                        ((CancellationTokenSource) state).Cancel();
                        _logger.Trace("operation timeout -> request for cancelation success");
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorException("cannot cancel task", ex);
                    }
                }
            }, tokenSource, timeout, Timeout.Infinite);
        }

        private void StopTaskTimer(ref Timer timer)
        {
            lock (_syncRoot4StopTaskTimer)
            {
                if (timer == null) return;
                timer.Dispose();
                timer = null;
            }
        }

        /// <summary>
        /// Cancels such operation as Init, EnableBillType, etc
        /// </summary>
        [UsedImplicitly]
        public void CancelMacroOperation()
        {
            lock (_syncRoot4MacroOperationExecutingState)
                if (_cancellationTokenSource != null)
                    _cancellationTokenSource.Cancel();
        }

        private void WaitForNoEvent(CancellationToken ct)
        {
            _logger.Debug("start");
            while (_newEvent)
            {
                if (!ct.IsCancellationRequested) continue;
                _logger.Debug("cancelation requested -> break");
                break;
            }
            _logger.Debug("finish");
        }

        private void WaitForReady(CancellationToken ct/*, int timeout = 2000*/)
        {
            _logger.Debug("start");
            //var tout = _readyEvent.WaitOne(timeout);
            while (!IsReady)
            {
                ct.ThrowIfCancellationRequested();
                Thread.Sleep(50);
                //throw new Exception("Waiting for the device completed after timeout");
            }
            _logger.Debug("finish");
        }

        private void WaitForEvent(CancellationToken ct, PollingEventType eventType)
        {
            _logger.Debug(eventType);
            var eventOccured = false;
            do
            {
                _logger.Trace("attempt to lock _syncRoot4LastEvents");
                lock (_syncRoot4LastEvents)
                {
                    _logger.Trace("success lock _syncRoot4LastEvents");
                    for (var i = _eventLog.Count - 1; i >= 0; i--)
                    {
                        if (_eventLog.ElementAt(i).IsUsedAfterWaiting)
                            continue;
                        // first unhandled event
                        _eventLog.ElementAt(i).IsUsedAfterWaiting = true;
                        if (_eventLog.ElementAt(i).EventType != eventType) 
                            continue;
                        eventOccured = true;
                        break;
                    }
                }
                _logger.Trace("leaving of the critical section _syncRoot4LastEvents");
                Thread.Sleep(50);
            } while (!ct.IsCancellationRequested && !eventOccured);
            _logger.Debug(eventOccured ? "Events is occured" : "waiting canceled");
        }

        #endregion

        #region Macro Operations

        /// <summary> Syncronization object for init operation </summary>
        [UsedImplicitly] public object SyncRoot4InitOperation = new object();

        /// <summary> InitOperation </summary>
        [UsedImplicitly] public Task<OperationResult<object>> InitOperation;

        private readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);

        /// <summary>
        /// Initialization
        /// </summary>
        /// <returns>Common implementation of operation result with null value</returns>
        [UsedImplicitly]
        public Task<OperationResult<object>> Init(int timeout)
        {
            return ExecMacroOperation<object>(token => { Init_Internal(token); return null; }, timeout);
        } 

        private void Init_Internal(CancellationToken token)
        {
            WaitForReady(token);
            CategoryID = RequestEquipmentCategoryID();
            ProductCode = RequestProductCode();
            BuildCode = RequestBuildCode();
            ManufacturerID = RequestManufacturerID();
            SerialNumber = RequestSerialNumber();
            SoftwareRevision = RequestSoftwareRevision();
            CommandsVersion = RequestCommsRevision();
            OptionFlags = RequestOptionFlags();
            Checksums = GetChecksum();
            WaitForReady(token);
            SetDateTime(DateTime.Now);
            WaitForReady(token);
            ScalingFactor = RequestCountryScalingFactor("RU");
            CurrencyRevision = RequestCurrencyRevision("RU");
            for (var i = 1; i <= 21; i++)
            {
                BillTypes[i] = RequestBillType(i);
                RaisePropertyUpdatedEvent("BillTypes");
            }
            WaitForReady(token);
            ChangeOperationMode(OperationMode.Sys_Game);
            WaitForReady(token);
            var ris = RequestInhibitStatus();
            ModifyInhibitStatus(ris);
            WaitForReady(token);
            RequestInhibitStatus();
            ReadFillsize();
            WaitForReady(token);
            ModifyMasterInhibitStatus(false);
            WaitForReady(token);
            MasterDispense(0, BillTypeToDispense.Test_Dispense, Paymode.Real,
                           PayoutStrategie.BIGGEST_BILL_FIRST, 0);
        }

        /// <summary>
        /// ResetDevice
        /// </summary>
        [UsedImplicitly]
        public Task<OperationResult<object>> ResetDevice(int timeout)
        {
            return ExecMacroOperation<object>(ct =>
            {
                ResetDevice();
                Init_Internal(ct);
                return null;
            }, timeout);
        }

        /// <summary>
        /// Enables / Disables the specified bill types according to the mask
        /// </summary>
        /// <param name="mask"> Bit mask in lsb format. the maximum number of bill types is 21 (21 bits) but actually is needed about 6</param>
        /// <param name="timeout"> Timeout in milliseconds</param>
        /// <returns> Result of the operation with a value indicating the success of the operation</returns>
        [UsedImplicitly]
        public Task<OperationResult<bool>> EnableBillTypes(int mask, int timeout)
        {
            return ExecMacroOperation(ct =>
            {
                _logger.Debug("EnableBillType start");
                //Debug.WriteLine("{1} thread {0} EnableBillType start", Task.CurrentId, DateTime.Now.ToString("HH.mm.ss:fff"));
                WaitForNoEvent(ct);
                //Debug.WriteLine("{1} thread {0} ModifyInhibitStatus", Task.CurrentId, DateTime.Now.ToString("HH.mm.ss:fff"));
                ModifyInhibitStatus(mask);
                ModifyMasterInhibitStatus(mask != 0);
                WaitForEvent(ct, PollingEventType._CIDLE);
                WaitForEvent(ct, mask != 0 ? PollingEventType._CFREE : PollingEventType._CLOCKED);
                var retMask = RequestInhibitStatus();
                ReadFillsize();
                _logger.Debug("EnableBillType finish");
                //Debug.WriteLine("{1} thread {0} EnableBillTypes finish", Task.CurrentId, DateTime.Now.ToString("HH.mm.ss:fff"));
                return mask == retMask;
            }, timeout);
        } 

        /// <summary>
        /// Dispense single bill
        /// </summary>
        /// <param name="typeToDispense"></param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns></returns>
        [UsedImplicitly]
        public Task<OperationResult<DispenseResult>> Dispense(BillTypeToDispense typeToDispense, int timeout)
        {
            return ExecMacroOperation(ct =>
            {
                _logger.Info("start");
                WaitForReady(ct);
                var billCountBeforeDispense = BillTypes[(int)typeToDispense - 1].Fillsize;
                var result = MasterDispense(1, typeToDispense, Paymode.Real,
                    PayoutStrategie.BIGGEST_BILL_FIRST, 0);
                WaitForEvent(ct, PollingEventType._CMOVBILL);
                WaitForEvent(ct, PollingEventType._CIDLE);
                WaitForReady(ct);
                ReadFillsize();
                if (ct.IsCancellationRequested)
                {
                    var billCountAfterDispense = BillTypes[(int)typeToDispense - 1].Fillsize;
                    if ((billCountBeforeDispense - 1) == billCountAfterDispense)
                    {
                        throw new Exception("Bill was not dispensed");
                    }
                }
                _logger.Info("finish, result: " + result.IsOK);
                return result;
            }, timeout);
        }

        /// <summary>
        /// Modify bill type of dispenser
        /// </summary>
        /// <param name="billType"></param>
        /// <param name="country"></param>
        /// <param name="valueCode"></param>
        /// <param name="maxBillCount"></param>
        /// <param name="callback"></param>
        [UsedImplicitly]
        public void ModifyBillType(BillTypeToDispense billType, string country, string valueCode, int maxBillCount, Action callback)
        {
            ExecMacroOperation<object>(ct =>
                {
                    // switch to unload_mode
                    ChangeOperationMode(OperationMode.Sys_Unload);

                    // unload all bills from dispensertype
                    do
                    {
                        ReadFillsize();
                        if (BillTypes[(byte)billType].Fillsize == 0) break;
                        WaitForReady(ct);
                        MasterDispense(1, billType, Paymode.Real, PayoutStrategie.BIGGEST_BILL_FIRST, 0);
                        WaitForReady(ct);
                    } while (true);

                    // modify
                    ModifyBillType(billType, country, valueCode, maxBillCount);

                    // switch back operation mode to Game 
                    ChangeOperationMode(OperationMode.Sys_Game);
                    Init_Internal(ct);
                    return null;
                });
        }

        #endregion
    }
}
