namespace IDM.Message
{
    public enum MSGHeader : int
    {
        Signature = 0,
        Event = 2
    }

    public enum MSG
    {
        Download = 14
    }

    public enum MSGField
    {
        URL = 6,
        Origin = 7,
        Referrer = 50,
        Cookies = 51,
        UserAgent = 54,

        Unk = -1
    }

    public enum MSGFieldType
    {
        String,
        Int
    }

}