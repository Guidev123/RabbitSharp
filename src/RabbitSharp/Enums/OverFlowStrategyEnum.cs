using System.ComponentModel;

namespace RabbitSharp.Enums
{
    public enum OverFlowStrategyEnum
    {
        [Description("reject-publish")]
        RejectPublish,

        [Description("reject-publish-dlx")]
        RejectPublishDlx,

        [Description("drop-head")]
        DropHead,
    }
}