using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Sms.SmsIr.Controllers;

public sealed class SmsIrConfigurationModel
{
    [DataType(DataType.Password)] public string? ApiKey { get; set; }
    public bool HasApiKey { get; set; }
    public bool Sandbox { get; set; }
    public int TemplateId { get; set; }
    public string CodeParameterName { get; set; } = "CODE";
    public long LineNumber { get; set; }
    public string? Warning { get; set; }
}

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public sealed class SmsIrController(SmsIrSettings settings, ISettingService settingService,
    IEncryptionService encryption, INotificationService notifications, IWebHostEnvironment environment, SmsIrRuntimeState runtime) : BasePluginController
{
    [HttpGet]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SMS_SETTINGS)]
    public IActionResult Configure() => View("~/Plugins/Sms.SmsIr/Views/Configure.cshtml", new SmsIrConfigurationModel
    {
        HasApiKey = !string.IsNullOrWhiteSpace(settings.EncryptedApiKey),
        Sandbox = settings.Sandbox, TemplateId = settings.TemplateId, CodeParameterName = settings.CodeParameterName,
        LineNumber = settings.LineNumber, Warning = settings.ConfigurationWarning(environment.IsDevelopment())
    });

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SMS_SETTINGS)]
    public async Task<IActionResult> Configure(SmsIrConfigurationModel model)
    {
        if (model.TemplateId <= 0) ModelState.AddModelError(nameof(model.TemplateId), "شناسهٔ قالب باید مثبت باشد.");
        if (!Regex.IsMatch(model.CodeParameterName ?? "", "^[A-Za-z][A-Za-z0-9_]{0,39}$"))
            ModelState.AddModelError(nameof(model.CodeParameterName), "نام پارامتر را بدون # وارد کنید، مثلاً CODE.");
        if (model.LineNumber < 0) ModelState.AddModelError(nameof(model.LineNumber), "شماره خط معتبر نیست.");
        if (model.ApiKey?.Any(char.IsWhiteSpace) == true) ModelState.AddModelError(nameof(model.ApiKey), "کلید نباید فاصله داشته باشد.");
        if (!ModelState.IsValid)
        {
            ModelState.Remove(nameof(model.ApiKey));
            model.ApiKey = null;
            model.HasApiKey = !string.IsNullOrWhiteSpace(settings.EncryptedApiKey);
            return View("~/Plugins/Sms.SmsIr/Views/Configure.cshtml", model);
        }
        if (!string.IsNullOrWhiteSpace(model.ApiKey)) settings.EncryptedApiKey = encryption.EncryptText(model.ApiKey.Trim());
        settings.Sandbox = model.Sandbox;
        settings.TemplateId = model.TemplateId;
        settings.CodeParameterName = model.CodeParameterName!;
        settings.LineNumber = model.LineNumber;
        await settingService.SaveSettingAsync(settings);
        await runtime.WriteAsync();
        notifications.SuccessNotification("تنظیمات sms.ir ذخیره شد. اعتبار کلید و قالب هنگام ارسال بررسی می‌شود.");
        return RedirectToAction(nameof(Configure));
    }
}
