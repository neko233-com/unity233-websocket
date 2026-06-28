namespace Unity233.WebSocket
{
    public enum Ws233CloseCode : ushort
    {
        Normal = 1000,
        GoingAway = 1001,
        ProtocolError = 1002,
        UnsupportedData = 1003,
        NoStatus = 1005,
        Abnormal = 1006,
        InvalidPayload = 1007,
        PolicyViolation = 1008,
        MessageTooBig = 1009,
        MandatoryExtension = 1010,
        InternalError = 1011,
        TlsHandshakeFailed = 1015,
    }
}
