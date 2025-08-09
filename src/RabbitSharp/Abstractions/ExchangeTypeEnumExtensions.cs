using System.ComponentModel;
using System.Reflection;

namespace RabbitSharp.Abstractions
{
    internal static class ExchangeTypeEnumExtensions
    {
        public static string GetEnumDescription(this ExchangeTypeEnum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString()) ?? default!;

            DescriptionAttribute[] attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[]
                ?? throw new ArgumentNullException();

            return attributes is not null && attributes.Length != 0 ? attributes.First().Description : value.ToString();
        }
    }
}