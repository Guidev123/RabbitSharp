using RabbitSharp.Enums;

namespace RabbitSharp.Abstractions
{
    public class SimpleNamingConventions : INamingConventions
    {
        public virtual string ExchangeNamingConvention(Type messageType)
        {
            var name = messageType.Name ?? messageType.ToString();

            return name ?? string.Empty;
        }

        public virtual string RoutingKeyNamingConvention(Type messageType, ExchangeTypeEnum exchangeType = ExchangeTypeEnum.Topic)
        {
            var typeName = messageType.Name;

            return exchangeType switch
            {
                ExchangeTypeEnum.Direct => typeName,
                ExchangeTypeEnum.Topic => $"{typeName}.#",
                ExchangeTypeEnum.Fanout => string.Empty,
                _ => typeName
            };
        }
    }
}