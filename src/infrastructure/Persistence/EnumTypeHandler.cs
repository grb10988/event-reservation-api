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

    public override TEnum Parse(object value) => Enum.Parse<TEnum>((string)value);
}