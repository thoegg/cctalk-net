using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dk.CctalkLib.Misc
{
    /// <summary>
    /// Describes the current state of host device
    /// </summary>
    public enum MasterDeviceState
    {
        /// <summary>
        /// Waiting for user command
        /// </summary>
        Idle,
        /// <summary>
        /// Sending a message to the slave device
        /// </summary>
        Sending,
        /// <summary>
        /// Receiving a message from the slave device
        /// </summary>
        Receiving
    }

    /// <summary>
    /// Contains list of ccTalk protocol commands (ccTalk Generic Specification Part 3 v4.6)
    /// </summary>
    public enum ccTalkCommand
    {
        /// <summary>
        /// Slave device response
        /// </summary>
        ReturnMessage = 0,
        ResetDevice = 1,
        RequestCommsStatusVariables = 2,
        ClearCommsStatusVariables = 3,
        RequestCommsRevision = 4,
        NAK_Message = 5,
        BUSY_Message = 6,

        SwitchEncryptionKey = 110,
        RequestEcryptionSupport = 111,

        SwitchBaudRate = 113,
        RequestUSBid = 114,

        StoreEncryptionCode = 136,
        SwitchEncryptionCode = 137,
        RequestCurrencyRevision = 145,
        RequestBillOperatingMode = 152,
        ModifyBillOperatingMode = 153,
        RequestCountryScalingFactor = 156,
        RequestBillId = 157,
        ModifyBillId = 158,
        ReadBufferedBillEvents = 159,
        RequestAddressMode = 169,
        RequestBaseYear = 170,
        RequestBuildCode = 192,

        CalculateROMChecksum = 197,

        RequestOptionFlags = 213,

        ReadBufferedCreditOrErrorCodes = 229,
        ModifyMasterInhibitStatus = 228,
        RequestInhibitStatus = 230,
        ModifyInhibitStatus =231, 
        RequestSoftwareRevision = 241,
        RequestSerialNumber = 242,

        RequestProductCode = 244,
        RequestEquipmentCategoryID = 245,
        RequestManufacturerID = 246,

        SimplePoll = 254
    }

}
