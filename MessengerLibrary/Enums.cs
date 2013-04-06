using System;

namespace MessengerLibrary
{

    public enum LogoutReason
    {
        InitiatedByUser,
        LoggedInElsewhere,
        ServerShuttingDown,
        ConnectionError,
    }

    public enum MessageOption
    {
        NoAcknoweldgement,
        Acknowledgement,
        NegativeAcknowledgementOnly,
        Data,
    }

    public enum ServerError
    {
        Unknown = 0,
        InvalidPrincipal = 205,
        NicknameChangeIllegal = 209,
        PrincipalListFull = 210,
        PrincipalAlreadyOnList = 215,
        PrincipalNotOnList = 216,
        PrincipalNotOnline = 217,
        AlreadyInMode = 218,
        TooManyGroups = 223,
        InvalidGroup = 224,
        PrincipalNotInGroup = 225,
        InternalServerError = 500,
        ChallengeResponseFailed = 540,
        ServerIsUnavailable = 601,
        CallingTooRapidly = 713,
        IllegalPropertyValue = 715,
        ChangingTooRapidly = 800,
        ServerTooBusy = 910,
        AuthenticationFailed = 911,
    }

    public enum PrivacySetting
    {
        AcceptInvitations,
        AddUsers,
    }

    public enum UserStatus
    {
        Offline,
        Online,
        Away,
        Busy,
        BeRightBack,
        Lunch,
        Phone,
        Invisible,
        Idle,
    }

    public enum UserProperty
    {
        HomePhone,
        WorkPhone,
        MobilePhone,
        AuthorizedMobile,
        MobileDevice,
        MSNDirectDevice,
        HasBlog,
    }

    [Flags]
    public enum UserCapabilities : uint
    {
        None = 0x00000000,
        MobileOnline = 0x00000001,
        MSN8User = 0x00000002,
        RendersGif = 0x00000004,
        RendersIsf = 0x00000008,
        WebCamDetected = 0x00000010,
        SupportsChunking = 0x00000020,
        MobileEnabled = 0x00000040,
        DirectDevice = 0x00000080,
        SupportsActivities = 0x00000100,
        WebIMClient = 0x00000200,
        MobileDevice = 0x00000400,
        ConnectedViaTGW = 0x00000800,
        HasSpace = 0x00001000,
        MCEUser = 0x00002000,
        SupportsDirectIM = 0x00004000,
        SupportsWinks = 0x00008000,
        SupportsMSNSearch = 0x00010000,
        IsBot = 0x00020000,
        SupportsVoiceIM = 0x00040000,
        SupportsSChannel = 0x00080000,
        SupportsSipInvite = 0x00100000,
        SupportsTunneledSip = 0x00200000,
        SupportsSDrive = 0x00400000,
        SupportsPageModeMessaging = 0x00800000,
        HasOnecare = 0x01000000,
        P2PSupportsTurn = 0x02000000,
        P2PBootstrapViaUUN = 0x04000000,
        UsingAlias = 0x08000000,
        Version1 = 0x10000000,
        Version2 = 0x20000000,
        Version3 = 0x30000000,
        Version4 = 0x40000000,
        Version5 = 0x50000000,
        Version6 = 0x60000000,
        Version7 = 0x70000000,
        Version8 = 0x80000000,
        Version9 = 0x90000000,
        Version10 = 0xA0000000,
        Version11 = 0xB0000000,
        Version12 = 0xC0000000,
    }



}


