using System.ComponentModel;
using System.Reflection;

namespace RabbitSharp.Extensions
{
    internal static class EnumDescriptionExtensions
    {
        public static string GetEnumDescription(this Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString()) ?? default!;

            DescriptionAttribute[] attributes = fi.GetCustomAttributes(typeof(DescriptionAttribute), false) as DescriptionAttribute[]
                ?? throw new ArgumentNullException();

            return attributes is not null && attributes.Length != 0 ? attributes.First().Description : value.ToString();
        }
    }
}