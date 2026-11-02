namespace SwarmSim.Tests;

// Fixed data only. Tests emit every actual checkpoint but never write or accept new references.
internal static class VicsekGoldenTraceFixtures
{
    internal static string Provenance => OperatingSystem.IsWindows()
        ? "Windows reference not recorded; capture the complete failing trace for review."
        : "Ubuntu 24.04.3 LTS / X64 / .NET 8.0.33 / SDK 8.0.427 (recorded 2026-11-02)";

    internal static GoldenCheckpoint[]? Trace => OperatingSystem.IsWindows() ? Windows : Linux;

    internal static readonly GoldenCheckpoint[] Linux =
    {
        new(0, "A051D748A5B4400A8189FFA6B166818C449C516FB6F4C945083E3DB08863A7DF", new uint[]
        {
            0x3E800000u, 0x3E800000u, 0x3E000000u, 0x00000000u,
            0x417C0000u, 0x413C0000u, 0x3E000000u, 0x00000000u,
            0x40800000u, 0x40800000u, 0x3E000000u, 0x00000000u,
            0x40B00000u, 0x40800000u, 0x3E000000u, 0x00000000u,
            0x40E00000u, 0x40800000u, 0x3E000000u, 0x00000000u,
            0x41000000u, 0x41100000u, 0x3E000000u, 0x00000000u,
            0x41200000u, 0x41100000u, 0x3E000000u, 0x00000000u,
            0x41500000u, 0x40C00000u, 0x3E000000u, 0x00000000u,
        }),
        new(1, "9078F999A7F268D165E1740B886720B25F5AE98608B3B64006A56A06E0244BC2", new uint[]
        {
            0x3EC00000u, 0x3E800000u, 0x3DEEC93Du, 0xBD3893D3u,
            0x417E0000u, 0x413C0000u, 0x3DF609A8u, 0xBD0D7116u,
            0x40840000u, 0x40800000u, 0x3DF7D682u, 0xBD004166u,
            0x40B40000u, 0x40800000u, 0x3DF23E67u, 0x3D2593CCu,
            0x40E40000u, 0x40800000u, 0x3DFF919Cu, 0xBBEDA32Au,
            0x41020000u, 0x41100000u, 0x3DF25DF7u, 0x3D24DAA3u,
            0x41220000u, 0x41100000u, 0x3DF87499u, 0xBCF6C478u,
            0x41520000u, 0x40C00000u, 0x3DF89135u, 0x3CF4F5C6u,
        }),
        new(16, "83CB61CCEC0B1C8BA606D3009CD8DD65747B089E51AA7F8E1D7EAFBD624AF14D", new uint[]
        {
            0x400CE03Au, 0x3E6BCEB0u, 0x3DE18849u, 0x3D723FC2u,
            0x3FD5767Au, 0x413B72E9u, 0x3DDBD1A9u, 0x3D833552u,
            0x40BD6946u, 0x40796CF3u, 0x3DF55739u, 0x3D123454u,
            0x40EDF482u, 0x408723B8u, 0x3DFC0C88u, 0x3CB33672u,
            0x410DF212u, 0x408F8E4Du, 0x3DFE231Fu, 0x3C769D30u,
            0x411BE3FDu, 0x411C9A9Bu, 0x3DE11C4Du, 0x3D73D039u,
            0x413AD6E5u, 0x411E6191u, 0x3DB0EFE2u, 0x3DB902F6u,
            0x416A4C7Au, 0x40AFC332u, 0x3D94930Cu, 0xBDD0797Eu,
        }),
        new(64, "11E8B7F9D47CFE2538402A840E13927DA7058E9B3B601010ED608B24A0DC67CF", new uint[]
        {
            0x40E94AE3u, 0x3FB09A79u, 0x3C393B56u, 0x3DFEF365u,
            0x40D222DAu, 0x3F7C5936u, 0x3D29AD6Bu, 0x3DF1888Eu,
            0x4137683Du, 0x40B096C9u, 0x3DD573CCu, 0x3D8D546Bu,
            0x414FE003u, 0x40B845EFu, 0x3D16CB27u, 0x3DF4A533u,
            0x416680BAu, 0x40B99319u, 0x3D8C691Bu, 0x3DD60EE0u,
            0x4146A498u, 0x40073C15u, 0xBCD29341u, 0x3DFA8759u,
            0x415FBF86u, 0x401A06E0u, 0xBBCE1EA0u, 0x3DFFACF8u,
            0x4160F88Du, 0x4079F6FCu, 0x3D25E325u, 0x3DF230D4u,
        }),
        new(256, "2865E6F0B3DED8B17890B6076337DF890291EC18023CDD7BD98DA4C381478C9C", new uint[]
        {
            0x40BE251Fu, 0x40087DE5u, 0xBD6070AAu, 0x3DE617BDu,
            0x40A6F118u, 0x4005ABE5u, 0xBD89CE88u, 0x3DD7BE5Cu,
            0x40B0B997u, 0x3F2314E8u, 0xBD9E4C57u, 0x3DC930CEu,
            0x40F2E762u, 0x3F68F287u, 0xBD0E655Au, 0x3DF5E66Cu,
            0x41134509u, 0x3F0D6620u, 0xBD6CECECu, 0x3DE2F11Au,
            0x40CEB7F2u, 0x40D4EDF5u, 0xBDFF218Cu, 0x3C289965u,
            0x40E0274Fu, 0x40DE7869u, 0xBDFDBFE0u, 0xBC877B0Cu,
            0x40D03AEFu, 0x4105B067u, 0xBDFB5362u, 0xBCC2C98Fu,
        }),
    };
    internal static readonly GoldenCheckpoint[]? Windows = null;
}
