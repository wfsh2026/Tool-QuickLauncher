$ErrorActionPreference = 'Stop'
$logPath = Join-Path -Path $PSScriptRoot -ChildPath 'update.log'
$session = $null
$parent = $null
$replaced = $false
$parentExited = $false

function Write-UpdateLog([string] $message) {
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$timestamp] $message`r`n"
    try {
        [System.IO.File]::AppendAllText($logPath, $line)
    }
    catch {
        # 日志故障不应触发对已经启动的新程序进行回滚。
        Write-Warning $line
    }
}

function Start-Launcher([string] $path) {
    $directory = Split-Path -LiteralPath $path
    $process = Start-Process -FilePath $path -WorkingDirectory $directory -WindowStyle Hidden -PassThru
    if ($null -eq $process) {
        throw '无法启动程序。'
    }
    $process.Dispose()
}

try {
    $configurationPath = Join-Path -Path $PSScriptRoot -ChildPath 'session.json'
    $configuration = [System.IO.File]::ReadAllText($configurationPath)
    $session = ConvertFrom-Json -InputObject $configuration
    Write-UpdateLog '更新进程已启动，正在等待主程序退出。'
    try {
        $parent = [System.Diagnostics.Process]::GetProcessById($session.ParentProcessId)
    }
    catch [System.ArgumentException] {
        # 主程序可能已经退出。
    }
    if ($null -ne $parent) {
        $startTime = $parent.StartTime
        $fileTime = $startTime.ToFileTimeUtc()
        $expectedTime = [long]::Parse($session.ParentStartTime)
        if ($fileTime -ne $expectedTime) {
            $parent.Dispose()
            $parent = $null
        }
    }
    [System.IO.File]::WriteAllText($session.ReadyPath, 'ready')
    if ($null -ne $parent) {
        $exited = $parent.WaitForExit(120000)
        if (-not $exited) {
            throw '旧程序在两分钟内未退出，已取消更新。请关闭异常提示或退出程序后重试。'
        }
        $parent.Dispose()
        $parent = $null
    }
    $parentExited = $true

    $sourceHash = Get-FileHash -LiteralPath $session.SourcePath -Algorithm SHA256
    if ($sourceHash.Hash -ne $session.SourceHash) {
        throw '下载文件在交接后发生变化，已取消更新。'
    }
    [System.IO.File]::Copy($session.SourcePath, $session.StagedPath, $false)
    Write-UpdateLog '旧程序已退出，开始备份和替换。'
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($true) {
        try {
            # 暂存文件与目标文件位于同一目录，用系统替换操作保留旧文件。
            [System.IO.File]::Replace($session.StagedPath, $session.TargetPath, $session.BackupPath)
            $replaced = $true
            break
        }
        catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw
            }
            Start-Sleep -Milliseconds 500
        }
    }

    Write-UpdateLog "替换成功。旧版本备份：$($session.BackupPath)"
    Start-Launcher $session.TargetPath
    Write-UpdateLog '已发起新版本启动，更新日志和旧版本备份已保留。'
    # 清理失败不应触发回滚正在运行的新程序。
    try {
        [System.IO.File]::Delete($session.SourcePath)
        [System.IO.File]::Delete($session.ReadyPath)
    }
    catch {
        Write-UpdateLog "更新成功，但临时文件清理失败：$_"
    }
}
catch {
    $failure = $_.ToString()
    try {
        Write-UpdateLog "更新失败：$failure"
        if ($replaced) {
            $failedPath = $session.StagedPath
            [System.IO.File]::Replace($session.BackupPath, $session.TargetPath, $failedPath)
            Write-UpdateLog '无法启动新程序，已恢复旧版本。'
            Start-Launcher $session.TargetPath
        }
        else {
            Write-UpdateLog '未完成文件替换，请检查目录权限，以及是否有其他 QuickLauncher 实例占用文件。'
            if ($parentExited) {
                Start-Launcher $session.TargetPath
                Write-UpdateLog '已重新启动原程序。'
            }
        }
    }
    catch {
        $rollbackFailure = $_.ToString()
        try { Write-UpdateLog "记录错误或恢复旧版本失败：$rollbackFailure" } catch { }
    }
    $quotedLogPath = '"' + $logPath + '"'
    # 失败时打开可见日志，避免隐藏更新进程静默退出。
    Start-Process -FilePath 'notepad.exe' -ArgumentList $quotedLogPath
    exit 1
}
finally {
    if ($null -ne $parent) {
        $parent.Dispose()
    }
}
