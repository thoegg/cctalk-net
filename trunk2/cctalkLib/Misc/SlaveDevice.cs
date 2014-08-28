using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dk.CctalkLib.Annotations;
using dk.CctalkLib.Devices;

namespace dk.CctalkLib.Misc
{
    /// <summary>
    /// Represents generic slave device for ccTalk protocol
    /// </summary>
    public class SlaveDevice
    {
        /// <summary>
        /// Slave device ID (Address)
        /// </summary>
        public byte ID { private set; get; }
        /// <summary>
        /// Link to master device
        /// </summary>
        [UsedImplicitly]
        public MasterDevice MasterDevice { private set; get; }

        /// <summary>
        /// .ctor
        /// </summary>
        /// <param name="id">Slave device ID (Address)</param>
        /// <param name="masterDevice">Link to master device</param>
        public SlaveDevice(byte id, MasterDevice masterDevice)
        {
            ID = id;
            MasterDevice = masterDevice;
        }

        /// <summary>
        /// Sends command to slave device. Preferable to use a method <see cref="ExecCommand"/>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="data"></param>
        [UsedImplicitly]
        protected void SendCommand(byte command, byte[] data = null)
        {
            MasterDevice.SendCommand(this, command, data);
        }

        /// <summary>
        /// Getting respponse from slave device. Preferable to use a method <see cref="ExecCommand"/>
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        [UsedImplicitly]
        protected Message ReceiveMessage(int timeout = MasterDevice.DefaultTimeout)
        {
            return MasterDevice.ReceiveMessage(timeout);
        }

        /// <summary>
        /// Sends command and receive response from slave device
        /// </summary>
        /// <param name="command"></param>
        /// <param name="timeout"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected Message ExecCommand(byte command, int timeout = MasterDevice.DefaultTimeout, byte[] data = null)
        {
            return MasterDevice.ExecCommand(this, command, timeout, data);
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void TerminateDevice(){}

        //  These are the commands which should be supported by all ccTalk peripherals. 
        //  They allow the device at the address specified to be precisely identified, even if the rest of the command set is unknown. 
        #region Core commmands
        /// <summary>
        /// RequestEcryptionSupport. Note: not implemented yet
        /// </summary>
        [UsedImplicitly]
        public void RequestEcryptionSupport()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// <para> RequestBuildCode with default timeout</para>
        /// <para> Slave response his id-string </para>
        /// </summary>
        [UsedImplicitly]
        public string RequestBuildCode()
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestBuildCode);
            return Encoding.ASCII.GetString(res.Data);
        }

        /// <summary>
        /// <para> RequestProductCode with default timeout</para>
        /// <para> Slave response his id-string </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public string RequestProductCode()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestProductCode);
            return Encoding.ASCII.GetString(res.Data);
        }

        /// <summary>
        /// <para>RequestEquipmentCategoryID with default timeout</para>
        /// <para>Transmitted data : [none] </para>
        /// <para>Received data :  ASCII </para>
        /// <para>The standard equipment category identification string is returned. Possible values are listed in Table 1 of cctalk Part 3 v4.6, 
        /// In fact, some devices can return values ​​other than those specified in the table </para>
        /// </summary> 
        [UsedImplicitly]
        public string RequestEquipmentCategoryID()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestEquipmentCategoryID);
            var catIDstr = Encoding.ASCII.GetString(res.Data);
            return catIDstr;
        }

        /// <summary>
        /// <para> RequestManufacturerID with default timeout</para>
        /// <para> Slave response his manufacture </para>
        /// </summary>
        [UsedImplicitly]
        public string RequestManufacturerID()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestManufacturerID);
            return Encoding.ASCII.GetString(res.Data);
        }

        /// <summary>
        /// <para> SimplePoll</para>
        /// <para>This command can be used to check that the slave device is powered-up and working. 
        /// No data is returned other than the standard ACK message and no action is performed. 
        /// It can be used at EMS ( Early Morning Start-up ) to check that the slave device is communicating. 
        /// A timeout on this command indicates a faulty or missing device, or an incorrect bus address or baud rate. </para>
        /// </summary>
        [UsedImplicitly]
        public bool SimplePoll()
        {
            var res = ExecCommand((byte)ccTalkCommand.SimplePoll);
            return res.Header == 0;
        }
        #endregion

        #region Core Plus commands

        /// <summary>
        /// <para>Transmitted data : [none] </para>
        /// <para>Received data : ACK </para>
        /// <para>This command forces a soft reset in the slave device. It is up to the slave device what action is taken on receipt of this command and whether any internal house-keeping is done. 
        /// The action may range from a jump to the reset vector to a forced physical reset of the processor and peripheral devices. 
        /// This command is worth trying before a hard reset ( or power-down where there is no reset pin ) is performed. </para>
        /// <para>The slave device should return an ACK immediately prior to resetting and allow enough time for the message to be sent back in full. 
        /// The host device should wait an appropriate amount of time after issuing a ‘Reset device’ command before sending the next command. </para>
        /// <para>Refer to the product manual for the reset initialisation time. </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public void ResetDevice()
        {
            ExecCommand((byte)ccTalkCommand.ResetDevice);
        }

        /// <summary>
        /// <para>Transmitted data : [none] </para>
        /// <para>Received data :  [ release ] [ major revision ] [ minor revision ] </para>
        /// <para>This command requests the ccTalk release number and the major / minor revision numbers of the comms specification. 
        /// This is read separately to the main software revision number for the product which can be obtained with a 'Request software revision' command.</para> 
        /// <para>The revision numbers should tie up with those at the top of this specification document. </para>
        /// <para>The ccTalk release number is used to track changes to the serial comms library in that particular product. </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public ccTalkVersion RequestCommsRevision()
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestCommsRevision);
            return new ccTalkVersion {Release = res.Data[0], MajorRevision = res.Data[1], MinorRevision = res.Data[2]};
        }

        /// <summary>
        /// 
        /// </summary>
        public struct ccTalkVersion
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public byte Release;
            /// <summary> </summary>
            [UsedImplicitly]
            public byte MajorRevision;
            /// <summary> </summary>
            [UsedImplicitly]
            public byte MinorRevision;
        }

        /// <summary>
        /// <para>Transmitted data :  [none] </para>
        /// <para>Received data :  [ serial 1 ] [ serial 2 ] [ serial 3 ] </para>
        /// <para>This command returns the device serial number in binary format and for most products a 3 byte code is sufficient. </para>        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        protected byte[] RequestSerialNumber()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestSerialNumber);
            return res.Data;
        }

        /// <summary>
        /// <para>Transmitted data :  [none]</para>
        /// <para>Received data :  ASCII </para>
        /// <para>The slave device software revision is returned. There is no restriction on format - it may include full alphanumeric characters. </para>
        /// <para>Any change to slave software, however minor, should be reflected in this revision code. </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public string RequestSoftwareRevision()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestSoftwareRevision);
            var res2 = Encoding.ASCII.GetString(res.Data);
            return res2;
        }

        #endregion

        /// <summary>
        /// <para>This command returns a past history of event codes for a coin acceptor in a small data buffer. 
        /// This allows a host device to poll a coin acceptor at a rate lower than that of coin insertion and still not miss any credits or other events. </para>
        /// <para>The standard event buffer size is 10 bytes which at 2 bytes per event is enough to store the last 5 events. </para>
        /// <para>A new event ripples data through the return data buffer and the oldest event is lost.</para>
        /// </summary>
        [UsedImplicitly]
        public DeviceEventBuffer ReadBufferedBillEvents()
        {
            var res = ExecCommand((byte)ccTalkCommand.ReadBufferedBillEvents);
            if (res.DataLen < 11)
                throw new Exception("incomplete message while reading events");
            var res2 = new DeviceEventBuffer();
            res2.Counter = res.Data[0];
            var deviceEvents = new List<DeviceEvent>();
            for (var i = 0; i < 5; i++)
            {
                deviceEvents.Add(new DeviceEvent(res.Data[i * 2 + 1], res.Data[i * 2 + 2]));
            }
            res2.Events = deviceEvents.ToArray();
            return res2;
        }

        /// <summary>
        /// <para>Transmitted data :  [none] </para>
        /// <para>Received data :  [ option flags ] </para>
        /// <para>This command reads option flags ( single bit control variables ) from a slave device. </para>
        /// </summary>
        [UsedImplicitly]
        public byte RequestOptionFlags()
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestOptionFlags);
            return res.Data != null && res.Data.Length > 0 ? res.Data[0] : (byte)0;
        }

        /// <summary> </summary>
        public struct ScalingFactor
        {
            /// <summary> </summary>
            [UsedImplicitly]
            public byte ScalingFactor_LSB;
            /// <summary> </summary>
            [UsedImplicitly]
            public byte ScalingFactor_MSB;
            /// <summary> </summary>
            [UsedImplicitly]
            public byte DecimalPlaces;
        }

        /// <summary>
        /// <para>Transmitted data :  [ country char 1 ] [ country char 2 ] </para>
        /// <para>Received data :  [ scaling factor LSB ] [ scaling factor MSB ] [ decimal places ] </para>
        /// <para>This command requests the scaling factor and decimal places for the standard country code provided. </para>
        /// <para>If all the return bytes are zero then that country code is not supported. </para>
        /// <para>Example : credit = Value Code * scaling factor / (10 * dec places)  = x.xx  Countrycode </para>
        /// <para>          credit = 0001       *  100           / (10 * 2)           = 1.00  EU    </para>
        /// </summary>
        /// <param name="country">2 symbols max</param>
        /// <returns></returns>
        [UsedImplicitly]
        public ScalingFactor RequestCountryScalingFactor(string country)
        {
            if (country.Length > 2)
                throw new ArgumentOutOfRangeException("country", "The maximum length of the parameter is equal to 2");
            var res = ExecCommand((byte)ccTalkCommand.RequestCountryScalingFactor, MasterDevice.DefaultTimeout, Encoding.ASCII.GetBytes(country));
            return new ScalingFactor
                       {
                           ScalingFactor_LSB = res.Data != null && res.Data.Length > 0 ? res.Data[0] : (byte)0, 
                           ScalingFactor_MSB =res.Data != null && res.Data.Length > 1 ? res.Data[1] : (byte)0, 
                           DecimalPlaces =res.Data != null && res.Data.Length > 2 ? res.Data[2] : (byte)0
                       };
        }

        /// <summary>
        /// <para>The method of calculating the ROM checksum is not defined in this document and can be adapted to suit the slave device. 
        /// A simple ‘unsigned long’ addition can be used as the simplest method with the result displayed as 8 hex digits. 
        /// The start address and end address is left to the slave device. More powerful devices may choose to calculate a CRC checksum using a pre-determined seed value. </para>
        /// <para>There is currently no ccTalk mechanism to compare the returned checksum value with a master reference value. That operation must be done by the host.</para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public byte[] CalculateROMChecksum()
        {
            var res = ExecCommand((byte) ccTalkCommand.CalculateROMChecksum);
            return res.Data;
        }

        /// <summary>
        /// Format (a) 
        /// Transmitted data :  [none] 
        /// Received data :  ASCII 
        /// Format (b) 
        /// Transmitted data :  [ country char 1 ] [ country char 2 ] 
        /// Received data :  ASCII 
        /// If no parameters are sent then a general currency revision is returned. Otherwise, 2 
        /// ASCII characters identifying the country of interest can be sent to determine a revision code specific to that country. 
        /// If the country identifier is not recognised then the string ‘Unknown’ is returned. 
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public string RequestCurrencyRevision(string country)
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestCurrencyRevision,
                                  MasterDevice.DefaultTimeout,
                                  string.IsNullOrEmpty(country) ? null : Encoding.ASCII.GetBytes(country));
            return Encoding.ASCII.GetString(res.Data);
        }        /// <summary>
        /// <para>Transmitted data :  [ bill type ] </para>
        /// <para>Received data :  [ char 1 ] [ char 2 ] [ char 3 ]… </para>
        /// <para>Refer to the ‘Modify bill id’ command for more details</para>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [UsedImplicitly]
        public string RequestBillId(byte id)
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestBillId, MasterDevice.DefaultTimeout, new []{id});
            return Encoding.ASCII.GetString(res.Data);
        }

        /// <summary>
        /// <para>Transmitted data :  [ mode control mask ] </para>
        /// <para>Received data :  ACK </para>
        /// <para>This command controls whether various product features are used. </para>
        /// </summary>
        /// <param name="modeControlMask">
        /// <para>B0 - stacker </para>
        /// <para>B1 - escrow </para>
        /// <para>0 = do not use, 1 = use </para>
        /// </param>
        [UsedImplicitly]
        public void ModifyBillOperatingMode(byte modeControlMask)
        {
            ExecCommand((byte)ccTalkCommand.ModifyBillOperatingMode, MasterDevice.DefaultTimeout, new[] { modeControlMask });
        }
 
        /// <summary>
        /// <para>Transmitted data : [none] </para>
        /// <para>Received data :  [ mode control mask ] </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public byte RequestBillOperatingMode()
        {
            var res = ExecCommand((byte) ccTalkCommand.RequestBillOperatingMode);
            return res.Data[0];
        }

        /// <summary>
        /// <para>Transmitted data :  [ inhibit mask 1 ] [ inhibit mask 2 ]… </para>
        /// <para>Received data :  ACK </para>
        /// <para>This command sends an individual inhibit pattern to a coin acceptor or bill validator. 
        /// With a 2 byte inhibit mask, up to 16 coins or bills can be inhibited or enabled. </para>
        /// </summary>
        /// <param name="masks"></param>
        [UsedImplicitly]
        public void ModifyInhibitStatus(byte[] masks)
        {
            ExecCommand((byte) ccTalkCommand.ModifyInhibitStatus, MasterDevice.DefaultTimeout, masks);
        }

        /// <summary>
        /// <para>Transmitted data :  [none] </para>
        /// <para>Received data :  [ inhibit mask 1 ] [ inhibit mask 2 ]… </para>
        /// <para>This command requests an individual inhibit pattern from a coin acceptor or bill validator. </para>
        /// </summary>
        /// <returns></returns>
        [UsedImplicitly]
        public byte[] RequestInhibitStatus()
        {
            var res = ExecCommand((byte)ccTalkCommand.RequestInhibitStatus);
            return res.Data;
        }
        /// <summary>
        /// <para>Transmitted data :  [ XXXXXXX | master inhibit status ] </para>
        /// <para>Received data :  ACK </para>
        /// <para>This command changes the master inhibit status in the slave device. In a coin acceptor, if the master inhibit is active then no coins can be accepted. Likewise for a bill validator. </para>
        /// </summary>
        /// <param name="mask">
        /// <para>[ master inhibit status ] </para>
        /// <para>Bit 0 only is used. </para>
        /// <para>0 - master inhibit active </para>
        /// <para>1 - normal operation </para>
        /// </param>
        [UsedImplicitly]
        public void ModifyMasterInhibitStatus(bool mask)
        {
            ExecCommand((byte) ccTalkCommand.ModifyMasterInhibitStatus, MasterDevice.DefaultTimeout, new[]{(byte)(mask ? 1 : 0)});
        }

        /// <summary>
        /// <para>Transmitted data :  [ bill type ] [ char 1 ] [ char 2 ] [ char 3 ]… </para>
        /// <para>Received data :  ACK </para>
        /// <para>[ bill type ] </para>
        /// <para>e.g. 1 to 16 </para>
        /// <para>[ C ] [ C ] [ V ] [ V ] [ V ] [ V ] [ I ] </para>
        /// <para>CC  = Standard 2 letter country code e.g. GB for the U.K. ( Great Britain ) </para>
        /// <para>VVVV  = Bill value in terms of the country scaling factor </para>
        /// <para>I  = Issue code. Starts at A and progresses B, C, D, E… </para>
        /// <para>See Appendix 10 for country codes and Appendix 15 for more information on this command. </para>
        /// </summary>
        /// <param name="billType"></param>
        /// <param name="value"></param>
        [UsedImplicitly]
        public void ModifyBillId(byte billType, string value)
        {
            ExecCommand((byte) ccTalkCommand.ModifyBillId, MasterDevice.DefaultTimeout, new [] {billType}.Concat(Encoding.ASCII.GetBytes(value)).ToArray());
        }
    }
}
