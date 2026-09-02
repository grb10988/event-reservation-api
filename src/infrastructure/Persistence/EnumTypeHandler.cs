using System.Data;
using Dapper;

namespace EventReservation.Infrastructure.Persistence;

public sealed class EnumTypeHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
    where TEnum : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, TEnum value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override TEnum Parse(object value)
    {
        if (value is string strValue
            && Enum.TryParse<TEnum>(strValue, ignoreCase: true, out var parsedEnum)
            && Enum.IsDefined(parsedEnum))
            return parsedEnum;

        if (value is not null && value is not DBNull)
        {
            var stringValue = value.ToString();
            if (!string.IsNullOrEmpty(stringValue)
                && Enum.TryParse<TEnum>(stringValue, ignoreCase: true, out var convertedEnum)
                && Enum.IsDefined(convertedEnum))
                return convertedEnum;
        }

        return default;
    }
}