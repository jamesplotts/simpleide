' Utilities/FoldStateStore.vb - Persists code-folding expansion state across application restarts
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json

Namespace Utilities

    ''' <summary>
    ''' Persists per-file code-folding expansion state to disk so it survives closing and
    ''' reopening a file, and application restarts
    ''' </summary>
    ''' <remarks>
    ''' Backed by a single JSON file (foldstate.json) in the application's config folder,
    ''' keyed by normalized full file path. Kept separate from settings.json because fold
    ''' state can grow large across many files and changes far more often than app settings.
    ''' </remarks>
    Public Class FoldStateStore

        ' ===== Private Shared Fields =====
        Private Shared ReadOnly pLock As New Object()
        Private Shared pFilePath As String
        Private Shared pData As Dictionary(Of String, Dictionary(Of String, Boolean))
        Private Shared pIsLoaded As Boolean = False

        ' ===== Private Shared Methods =====

        ''' <summary>
        ''' Loads the backing JSON file into memory on first use
        ''' </summary>
        Private Shared Sub EnsureLoaded()
            If pIsLoaded Then Return

            SyncLock pLock
                If pIsLoaded Then Return

                Try
                    Dim lAppDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                    Dim lAppFolder As String = Path.Combine(lAppDataPath, "SimpleIDE")

                    If Not Directory.Exists(lAppFolder) Then
                        Directory.CreateDirectory(lAppFolder)
                    End If

                    pFilePath = Path.Combine(lAppFolder, "foldstate.json")

                    If File.Exists(pFilePath) Then
                        Dim lJson As String = File.ReadAllText(pFilePath)
                        pData = JsonSerializer.Deserialize(Of Dictionary(Of String, Dictionary(Of String, Boolean)))(lJson)
                    End If

                Catch ex As Exception
                    Console.WriteLine($"FoldStateStore.EnsureLoaded error: {ex.Message}")
                End Try

                If pData Is Nothing Then
                    pData = New Dictionary(Of String, Dictionary(Of String, Boolean))()
                End If

                pIsLoaded = True
            End SyncLock
        End Sub

        ''' <summary>
        ''' Normalizes a file path so the same file always maps to the same dictionary key
        ''' </summary>
        ''' <param name="vFilePath">Path to normalize</param>
        ''' <returns>Full, normalized path, or the original string if normalization fails</returns>
        Private Shared Function NormalizeKey(vFilePath As String) As String
            Try
                Return Path.GetFullPath(vFilePath)
            Catch
                Return vFilePath
            End Try
        End Function

        ' ===== Public Shared Methods =====

        ''' <summary>
        ''' Loads the persisted fold state for a single file
        ''' </summary>
        ''' <param name="vFilePath">Full path of the source file</param>
        ''' <returns>Dictionary mapping node path to expansion state; empty if nothing was persisted</returns>
        Public Shared Function Load(vFilePath As String) As Dictionary(Of String, Boolean)
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return New Dictionary(Of String, Boolean)()

                EnsureLoaded()

                SyncLock pLock
                    Dim lKey As String = NormalizeKey(vFilePath)
                    If pData.ContainsKey(lKey) Then
                        Return New Dictionary(Of String, Boolean)(pData(lKey))
                    End If
                End SyncLock

            Catch ex As Exception
                Console.WriteLine($"FoldStateStore.Load error: {ex.Message}")
            End Try

            Return New Dictionary(Of String, Boolean)()
        End Function

        ''' <summary>
        ''' Persists the fold state for a single file to disk
        ''' </summary>
        ''' <param name="vFilePath">Full path of the source file</param>
        ''' <param name="vState">Dictionary mapping node path to expansion state</param>
        Public Shared Sub Save(vFilePath As String, vState As Dictionary(Of String, Boolean))
            Try
                If String.IsNullOrEmpty(vFilePath) OrElse vState Is Nothing Then Return

                EnsureLoaded()

                SyncLock pLock
                    Dim lKey As String = NormalizeKey(vFilePath)
                    pData(lKey) = New Dictionary(Of String, Boolean)(vState)

                    Dim lJson As String = JsonSerializer.Serialize(pData, New JsonSerializerOptions With {.WriteIndented = True})
                    File.WriteAllText(pFilePath, lJson)
                End SyncLock

            Catch ex As Exception
                Console.WriteLine($"FoldStateStore.Save error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
