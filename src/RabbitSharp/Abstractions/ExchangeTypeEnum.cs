using System.ComponentModel;

namespace RabbitSharp.Abstractions
{
    public enum ExchangeTypeEnum
    {
        [Description("direct")]
        Direct,

        [Description("fanout")]
        Fanout,

        [Description("topic")]
        Topic
    }
}