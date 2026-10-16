using FluentMigrator;
using FluentMigrator.Builders.Create.Table;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Data.Migrations;

namespace Nop.Plugin.Sms.SmsIr.Data;

[NopMigration("2026-10-15 00:00:00", "Sms.SmsIr audit schema", MigrationProcessType.Installation)]
public sealed class SchemaMigration : Migration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<SmsIrMessage>();
        if (!Schema.Table(nameof(SmsIrMessage)).Index("IX_SmsIrMessage_Phone_Created").Exists())
            Create.Index("IX_SmsIrMessage_Phone_Created").OnTable(nameof(SmsIrMessage)).OnColumn(nameof(SmsIrMessage.Phone)).Ascending().OnColumn(nameof(SmsIrMessage.CreatedOnUtc)).Descending();
    }
    public override void Down() => this.DeleteTableIfExists<SmsIrMessage>();
}

public sealed class SmsIrMessageBuilder : NopEntityBuilder<SmsIrMessage>
{
    public override void MapEntity(CreateTableExpressionBuilder table) => table
        .WithColumn(nameof(SmsIrMessage.Phone)).AsString(20).NotNullable()
        .WithColumn(nameof(SmsIrMessage.Kind)).AsString(16).NotNullable()
        .WithColumn(nameof(SmsIrMessage.Outcome)).AsString(32).NotNullable()
        .WithColumn(nameof(SmsIrMessage.Reason)).AsString(500).NotNullable();
}
