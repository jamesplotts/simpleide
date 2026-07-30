' Utilities/FileSystemWatcher.vb - File system monitoring utility
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading
Imports System.Timers
Imports SimpleIDE.Managers

Namespace Utilities
    
    ' Custom file system watcher for monitoring external file changes
    Public Class FileSystemWatcher
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pWatchers As New Dictionary(Of String, System.IO.FileSystemWatcher)()
        Private pSettingsManager As SettingsManager
        Private pDebounceTimer As System.Timers.Timer
        Private pPendingChanges As New Dictionary(Of String, DateTime)()
        Private pSyncLock As New Object()
        Private pDisposed As Boolean = False
        
        ' ===== Events =====
        Public Event FileChanged(vFilePath As String)
        Public Event FileDeleted(vFilePath As String)
        Public Event FileRenamed(vOldPath As String, vNewPath As String)
        
        ' ===== Constructor =====
        Public Sub New(vSettingsManager As SettingsManager)
            Try
                pSettingsManager = vSettingsManager
                
                ' Setup debounce timer to avoid multiple events for single file change
                pDebounceTimer = New System.Timers.Timer(500) ' 500ms debounce
                pDebounceTimer.AutoReset = False
                AddHandler pDebounceTimer.Elapsed, AddressOf OnDebounceTimerElapsed
                
            Catch ex As Exception
                Console.WriteLine($"FileSystemWatcher constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ' Start watching a file
        Public Sub WatchFile(vFilePath As String)
            Try
                If String.IsNullOrEmpty(vFilePath) OrElse Not File.Exists(vFilePath) Then
                    Return
                End If
                
                ' Get directory to watch
                Dim lDirectory As String = Path.GetDirectoryName(vFilePath)
                Dim lFileName As String = Path.GetFileName(vFilePath)
                
                ' Check if we're already watching this directory
                If pWatchers.ContainsKey(lDirectory) Then
                    Return
                End If
                
                ' Create new watcher for the directory
                Dim lWatcher As New System.IO.FileSystemWatcher(lDirectory)
                lWatcher.Filter = "*.*"
                lWatcher.NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.FileName Or NotifyFilters.Size
                lWatcher.IncludeSubdirectories = False
                
                ' Wire up events
                AddHandler lWatcher.Changed, AddressOf OnFileChanged
                AddHandler lWatcher.Deleted, AddressOf OnFileDeleted
                AddHandler lWatcher.Renamed, AddressOf OnFileRenamed
                AddHandler lWatcher.Error, AddressOf OnWatcherError
                
                ' Start watching
                lWatcher.EnableRaisingEvents = True
                
                ' Add to dictionary
                pWatchers(lDirectory) = lWatcher
                
                Console.WriteLine($"Now watching directory: {lDirectory}")
                
            Catch ex As Exception
                Console.WriteLine($"WatchFile error: {ex.Message}")
            End Try
        End Sub
        
        ' Stop watching a file
        Public Sub UnwatchFile(vFilePath As String)
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return
                
                Dim lDirectory As String = Path.GetDirectoryName(vFilePath)
                
                If pWatchers.ContainsKey(lDirectory) Then
                    Dim lWatcher As System.IO.FileSystemWatcher = pWatchers(lDirectory)
                    lWatcher.EnableRaisingEvents = False
                    lWatcher.Dispose()
                    pWatchers.Remove(lDirectory)
                    
                    Console.WriteLine($"Stopped watching directory: {lDirectory}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UnwatchFile error: {ex.Message}")
            End Try
        End Sub
        
        ' Stop all watchers
        Public Sub StopAll()
            Try
                For Each lKvp In pWatchers
                    lKvp.Value.EnableRaisingEvents = False
                    lKvp.Value.Dispose()
                Next
                
                pWatchers.Clear()
                
            Catch ex As Exception
                Console.WriteLine($"StopAll error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - Event Handlers =====
        
        Private Sub OnFileChanged(vSender As Object, vArgs As FileSystemEventArgs)
            Try
                ' Debounce file changes to avoid multiple events
                SyncLock pSyncLock
                    pPendingChanges(vArgs.FullPath) = DateTime.Now
                    pDebounceTimer.Stop()
                    pDebounceTimer.Start()
                End SyncLock
                
            Catch ex As Exception
                Console.WriteLine($"OnFileChanged error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileDeleted(vSender As Object, vArgs As FileSystemEventArgs)
            Try
                Console.WriteLine($"File deleted: {vArgs.FullPath}")
                RaiseEvent FileDeleted(vArgs.FullPath)
                
            Catch ex As Exception
                Console.WriteLine($"OnFileDeleted error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnFileRenamed(vSender As Object, vArgs As RenamedEventArgs)
            Try
                Console.WriteLine($"File renamed: {vArgs.OldFullPath} -> {vArgs.FullPath}")
                RaiseEvent FileRenamed(vArgs.OldFullPath, vArgs.FullPath)
                
            Catch ex As Exception
                Console.WriteLine($"OnFileRenamed error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnWatcherError(vSender As Object, vArgs As ErrorEventArgs)
            Try
                Console.WriteLine($"FileSystemWatcher error: {vArgs.GetException().Message}")
                
                ' Try to restart the watcher
                Dim lWatcher As System.IO.FileSystemWatcher = TryCast(vSender, System.IO.FileSystemWatcher)
                If lWatcher IsNot Nothing Then
                    lWatcher.EnableRaisingEvents = False
                    Thread.Sleep(1000) ' Wait a bit
                    lWatcher.EnableRaisingEvents = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnWatcherError error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDebounceTimerElapsed(vSender As Object, vArgs As ElapsedEventArgs)
            Try
                Dim lChangedFiles As New List(Of String)()
                
                SyncLock pSyncLock
                    ' Get all pending changes
                    For Each lKvp In pPendingChanges
                        lChangedFiles.Add(lKvp.key)
                    Next
                    pPendingChanges.Clear()
                End SyncLock
                
                ' Raise events for changed files
                For Each lFilePath In lChangedFiles
                    Console.WriteLine($"File changed: {lFilePath}")
                    RaiseEvent FileChanged(lFilePath)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"OnDebounceTimerElapsed error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IDisposable Implementation =====
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
        
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not pDisposed Then
                If disposing Then
                    ' Dispose managed resources
                    StopAll()
                    
                    If pDebounceTimer IsNot Nothing Then
                        pDebounceTimer.Stop()
                        pDebounceTimer.Dispose()
                    End If
                    
                    pPendingChanges?.Clear()
                End If
                
                pDisposed = True
            End If
        End Sub
        
    End Class
    
End Namespace
