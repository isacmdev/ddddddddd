// <copyright file="TaskSchedulerBackupService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Threading.Tasks;
using ControlParental.Domain;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;

/// <summary>
/// T20 — Registers backup tasks with Windows Task Scheduler as a safety net
/// for when service timers fail. Uses the TaskScheduler NuGet package.
/// </summary>
public sealed class TaskSchedulerBackupService : ITaskSchedulerBackup, IDisposable
{
    private const string TaskPrefix = "ControlParental_Backup_";
    private const string HeartbeatTaskName = TaskPrefix + "Heartbeat";
    private const string OutboxTaskName = TaskPrefix + "Outbox";
    private const string ReconcileTaskName = TaskPrefix + "Reconcile";
    private const string BackupHeartbeatArg = "--backup-heartbeat";
    private const string BackupOutboxArg = "--backup-outbox";
    private const string BackupReconcileArg = "--backup-reconcile";
    private const int PeriodicIntervalMinutes = 15;

    private readonly object lockObj = new();
    private bool isRegistered;
    private bool disposed;

    /// <inheritdoc />
    public bool AreBackupTasksRegistered
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isRegistered;
            }
        }
    }

    /// <inheritdoc />
    public System.Threading.Tasks.Task<bool> RegisterBackupTasksAsync(CancellationToken ct = default)
    {
        if (this.disposed)
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }

        try
        {
            var result = this.RegisterTasksInternal();
            return System.Threading.Tasks.Task.FromResult(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Access denied registering tasks: {ex.Message}");
            return System.Threading.Tasks.Task.FromResult(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Failed to register tasks: {ex.Message}");
            return System.Threading.Tasks.Task.FromResult(false);
        }
    }

    private bool RegisterTasksInternal()
    {
        try
        {
            using var ts = new TaskService();

            // Register heartbeat backup task
            this.RegisterTask(
                ts,
                HeartbeatTaskName,
                "ControlParental heartbeat backup",
                BackupHeartbeatArg,
                new Trigger[]
                {
                    new BootTrigger(),
                    new LogonTrigger(),
                    new TimeTrigger { StartBoundary = DateTime.Now, Repetition = new RepetitionPattern(TimeSpan.FromMinutes(PeriodicIntervalMinutes), TimeSpan.Zero) },
                });

            // Register outbox push backup task
            this.RegisterTask(
                ts,
                OutboxTaskName,
                "ControlParental outbox push backup",
                BackupOutboxArg,
                new Trigger[]
                {
                    new BootTrigger(),
                    new LogonTrigger(),
                    new TimeTrigger { StartBoundary = DateTime.Now, Repetition = new RepetitionPattern(TimeSpan.FromMinutes(PeriodicIntervalMinutes), TimeSpan.Zero) },
                });

            // Register reconciliation backup task
            this.RegisterTask(
                ts,
                ReconcileTaskName,
                "ControlParental reconciliation backup",
                BackupReconcileArg,
                new Trigger[]
                {
                    new BootTrigger(),
                    new LogonTrigger(),
                    new TimeTrigger { StartBoundary = DateTime.Now, Repetition = new RepetitionPattern(TimeSpan.FromMinutes(PeriodicIntervalMinutes), TimeSpan.Zero) },
                });

            lock (this.lockObj)
            {
                this.isRegistered = true;
            }

            Debug.WriteLine("[TaskSchedulerBackup] All backup tasks registered successfully.");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Access denied: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Registration failed: {ex.Message}");
            return false;
        }
    }

    private void RegisterTask(TaskService ts, string taskName, string description, string argument, Trigger[] triggers)
    {
        var td = ts.NewTask();
        td.RegistrationInfo.Description = description;
        td.RegistrationInfo.Author = "ControlParental";
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.FromMinutes(5);
        td.Settings.AllowDemandStart = true;
        td.Settings.Enabled = true;
        td.Settings.Hidden = false;

        // Add triggers
        foreach (var trigger in triggers)
        {
            td.Triggers.Add(trigger);
        }

        // Add action - run the service executable with backup argument
        td.Actions.Add(new ExecAction(
            Program.ServiceExePath,
            argument,
            Path.GetDirectoryName(Program.ServiceExePath)));

        // Register with CreateOrUpdate policy and Ignore delete policy
        ts.RootFolder.RegisterTaskDefinition(
            taskName,
            td,
            TaskCreation.CreateOrUpdate,
            null, // userId
            null, // password
            TaskLogonType.InteractiveToken,
            null); // sddl

        Debug.WriteLine($"[TaskSchedulerBackup] Registered task: {taskName}");
    }

    /// <inheritdoc />
    public System.Threading.Tasks.Task<bool> UnregisterBackupTasksAsync(CancellationToken ct = default)
    {
        if (this.disposed)
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }

        try
        {
            var result = this.UnregisterTasksInternal();
            return System.Threading.Tasks.Task.FromResult(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Access denied unregistering tasks: {ex.Message}");
            return System.Threading.Tasks.Task.FromResult(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Failed to unregister tasks: {ex.Message}");
            return System.Threading.Tasks.Task.FromResult(false);
        }
    }

    private bool UnregisterTasksInternal()
    {
        try
        {
            using var ts = new TaskService();
            var taskNames = new[] { HeartbeatTaskName, OutboxTaskName, ReconcileTaskName };

            foreach (var taskName in taskNames)
            {
                try
                {
                    var task = ts.GetTask(taskName);
                    if (task != null)
                    {
                        ts.RootFolder.DeleteTask(taskName, false);
                        Debug.WriteLine($"[TaskSchedulerBackup] Deleted task: {taskName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TaskSchedulerBackup] Failed to delete task {taskName}: {ex.Message}");
                    // Individual task deletion failed — return false
                    return false;
                }
            }

            lock (this.lockObj)
            {
                this.isRegistered = false;
            }

            // No exceptions occurred — if tasks didn't exist, that's fine (return true)
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Access denied: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TaskSchedulerBackup] Unregistration failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
