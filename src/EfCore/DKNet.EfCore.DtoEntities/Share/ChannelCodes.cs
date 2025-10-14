using System.ComponentModel;

namespace DKNet.EfCore.DtoEntities.Share;

public enum ChannelCodes
{
    None,

    [Description(
        "Quick Response ChargeCode Indonesian Standard, universal payment system in Indonesia digital payment system.")]
    QrQris
}