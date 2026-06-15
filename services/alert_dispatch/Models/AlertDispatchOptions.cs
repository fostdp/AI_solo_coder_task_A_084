namespace TextileMonitoring.AlertDispatch.Models;

public class AlertDispatchOptions
{
    public AlertTemplateConfig Templates { get; set; } = new();
}

public class AlertTemplateConfig
{
    public string DingTalkTitleTemplate { get; set; } = "【{AlertLevel}】{Title}";
    public string DingTalkBodyTemplate { get; set; } = @"### {Title}

**告警级别**: {AlertLevel}
**文物名称**: {TextileName}
**告警类型**: {AlertType}

**当前值**: {ActualValue}
**阈值**: {Threshold}

**描述**: {Description}

**建议**: {Recommendation}

**时间**: {Timestamp:yyyy-MM-dd HH:mm:ss}";

    public string EmailSubjectTemplate { get; set; } = "[{AlertLevel}] {Title} - {TextileName}";
    public string EmailBodyTemplate { get; set; } = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <title>{Title}</title>
    <style>
        body { font-family: 'Microsoft YaHei', Arial, sans-serif; }
        .alert-box { padding: 20px; border-radius: 8px; margin-bottom: 20px; }
        .critical { background-color: #fee2e2; border-left: 4px solid #dc2626; }
        .high { background-color: #fed7aa; border-left: 4px solid #ea580c; }
        .medium { background-color: #fef3c7; border-left: 4px solid #d97706; }
        .low { background-color: #dbeafe; border-left: 4px solid #2563eb; }
        .label { font-weight: bold; color: #374151; }
        .value { color: #111827; }
    </style>
</head>
<body>
    <div class=""alert-box {AlertLevelClass}"">
        <h2>{Title}</h2>
        <p><span class=""label"">告警级别:</span> <span class=""value"">{AlertLevel}</span></p>
        <p><span class=""label"">文物名称:</span> <span class=""value"">{TextileName}</span></p>
        <p><span class=""label"">告警类型:</span> <span class=""value"">{AlertType}</span></p>
        <p><span class=""label"">当前值:</span> <span class=""value"">{ActualValue}</span></p>
        <p><span class=""label"">阈值:</span> <span class=""value"">{Threshold}</span></p>
        <p><span class=""label"">描述:</span> <span class=""value"">{Description}</span></p>
        <p><span class=""label"">建议:</span> <span class=""value"">{Recommendation}</span></p>
        <p><span class=""label"">时间:</span> <span class=""value"">{Timestamp:yyyy-MM-dd HH:mm:ss}</span></p>
    </div>
</body>
</html>";
}
