' AI/MemoryManifest.vb - AI memory management and manifest system
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace AI
    
    ' Manages AI memory operations and context tracking
    Public Class MemoryManifest
        Implements IDisposable
        
        ' ===== Private Fields =====
        Private pSettingsManager As SettingsManager
        Private pMem0Client As Mem0Client
        Private pMemoryCache As New Dictionary(Of String, MemoryEntry)()
        Private pSessionMemories As New List(Of String)()
        Private pIsInitialized As Boolean = False
        
        ' ===== Memory Entry Structure =====
        Public Class MemoryEntry
            Public Property key As String
            Public Property Value As String
            Public Property Category As MemoryCategory = MemoryCategory.eUnspecified
            Public Property Priority As MemoryPriority = MemoryPriority.eMedium
            Public Property CreatedAt As DateTime = DateTime.Now
            Public Property LastAccessed As DateTime = DateTime.Now
            Public Property AccessCount As Integer = 0
            Public Property Metadata As Dictionary(Of String, Object)
            Public Property IsUserGenerated As Boolean = False
            Public Property IsPersistent As Boolean = True
            
            Public Sub New()
                Metadata = New Dictionary(Of String, Object)()
            End Sub
            
            Public Sub New(vKey As String, vValue As String)
                Me.New()
                key = vKey
                Value = vValue
            End Sub
        End Class
        
        ' ===== Enums =====
        Public Enum MemoryCategory
            eUnspecified
            eUserPreference      ' User settings and preferences
            eProjectContext      ' project-specific information
            eCodePattern         ' code patterns and best practices
            eErrorResolution     ' error fixes and solutions
            eWorkflow            ' User workflow patterns
            eDocumentation       ' documentation and help Content
            ePersonalization     ' Personal AI assistant training
            eLastValue
        End Enum
        
        Public Enum MemoryPriority
            eUnspecified
            eLow                 ' Background information
            eMedium              ' Standard Context
            eHigh                ' Important patterns
            eCritical            ' Essential preferences
            eLastValue
        End Enum
        
        ' ===== Constructor =====
        Public Sub New(vSettingsManager As SettingsManager)
            Try
                pSettingsManager = vSettingsManager
                InitializeMemorySystem()
                
            Catch ex As Exception
                Console.WriteLine($"MemoryManifest constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization =====
        Private Sub InitializeMemorySystem()
            Try
                ' Initialize Mem0 client if API key is available
                Dim lApiKey As String = pSettingsManager.GetString("Mem0.ApiKey", "")
                If Not String.IsNullOrEmpty(lApiKey) Then
                    pMem0Client = New Mem0Client(lApiKey)
                    Console.WriteLine("MemoryManifest: Mem0 client initialized")
                End If
                
                ' Load cached memories
                LoadMemoryCache()
                
                pIsInitialized = True
                
            Catch ex As Exception
                Console.WriteLine($"InitializeMemorySystem error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ' Store a memory entry
        Public Async Function StoreMemory(vKey As String, vValue As String, 
                                         vCategory As MemoryCategory, 
                                         vPriority As MemoryPriority,
                                         Optional vMetadata As Dictionary(Of String, Object) = Nothing,
                                         Optional vIsPersistent As Boolean = True) As Task(Of Boolean)
            Try
                ' Create memory entry
                Dim lEntry As New MemoryEntry(vKey, vValue) With {
                    .Category = vCategory,
                    .Priority = vPriority,
                    .IsPersistent = vIsPersistent,
                    .IsUserGenerated = True
                }
                
                If vMetadata IsNot Nothing Then
                    For Each lKvp In vMetadata
                        lEntry.Metadata(lKvp.key) = lKvp.Value
                    Next
                End If
                
                ' Add to cache
                pMemoryCache(vKey) = lEntry
                pSessionMemories.Add(vKey)
                
                ' Store persistently if enabled and Mem0 client available
                If vIsPersistent AndAlso pMem0Client IsNot Nothing Then
                    Dim lSuccess As Boolean = Await pMem0Client.StoreMemoryAsync(vKey, vValue, lEntry.Metadata)
                    If Not lSuccess Then
                        Console.WriteLine($"Failed to store Memory persistently: {vKey}")
                    End If
                End If
                
                ' Save cache locally
                SaveMemoryCache()
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"StoreMemory error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Retrieve a memory entry
        Public Async Function RetrieveMemory(vKey As String) As Task(Of MemoryEntry)
            Try
                ' Check cache first
                If pMemoryCache.ContainsKey(vKey) Then
                    Dim lEntry As MemoryEntry = pMemoryCache(vKey)
                    lEntry.LastAccessed = DateTime.Now
                    lEntry.AccessCount += 1
                    Return lEntry
                End If
                
                ' Try to retrieve from Mem0 if available
                If pMem0Client IsNot Nothing Then
                    Dim lValue As String = Await pMem0Client.RetrieveMemoryAsync(vKey)
                    If Not String.IsNullOrEmpty(lValue) Then
                        Dim lEntry As New MemoryEntry(vKey, lValue) With {
                            .LastAccessed = DateTime.Now,
                            .AccessCount = 1
                        }
                        
                        ' Add to cache
                        pMemoryCache(vKey) = lEntry
                        Return lEntry
                    End If
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveMemory error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Search memories by pattern or content
        Public Async Function SearchMemories(vQuery As String, Optional vCategory As MemoryCategory = MemoryCategory.eUnspecified, Optional vLimit As Integer = 10) As Task(Of List(Of MemoryEntry))
            Try
                Dim lResults As New List(Of MemoryEntry)()
                
                ' Search local cache first
                For Each lKvp In pMemoryCache
                    Dim lEntry As MemoryEntry = lKvp.Value
                    
                    ' Apply category filter
                    If vCategory <> MemoryCategory.eUnspecified AndAlso lEntry.Category <> vCategory Then
                        Continue For
                    End If
                    
                    ' Check if query matches key or value
                    If lEntry.key.Contains(vQuery, StringComparison.OrdinalIgnoreCase) OrElse 
                       lEntry.Value.Contains(vQuery, StringComparison.OrdinalIgnoreCase) Then
                        lResults.Add(lEntry)
                    End If
                Next
                
                ' Search Mem0 if available and we need more results
                If lResults.Count < vLimit AndAlso pMem0Client IsNot Nothing Then
                    Try
                        Dim lMem0Results As List(Of Mem0Client.Memory) = Await pMem0Client.SearchMemoriesAsync(vQuery, vLimit - lResults.Count)
                        
                        For Each lMem0Memory In lMem0Results
                            ' Convert to MemoryEntry if not already in cache
                            If Not pMemoryCache.ContainsKey(lMem0Memory.key) Then
                                Dim lEntry As New MemoryEntry(lMem0Memory.key, lMem0Memory.Value) With {
                                    .CreatedAt = lMem0Memory.CreatedAt,
                                    .LastAccessed = DateTime.Now,
                                    .AccessCount = 1
                                }
                                lResults.Add(lEntry)
                                
                                ' Add to cache
                                pMemoryCache(lMem0Memory.key) = lEntry
                            End If
                        Next
                        
                    Catch ex As Exception
                        Console.WriteLine($"Mem0 search error: {ex.Message}")
                    End Try
                End If
                
                ' Sort by priority and last accessed
                lResults = lResults.OrderByDescending(Function(e) CInt(e.Priority) * 100 + e.AccessCount).Take(vLimit).ToList()
                
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"SearchMemories error: {ex.Message}")
                Return New List(Of MemoryEntry)()
            End Try
        End Function
        
        ' Store user preference
        Public Async Function StoreUserPreference(vPreference As String, vValue As String) As Task(Of Boolean)
            Return Await StoreMemory($"user.preference.{vPreference}", vValue, MemoryCategory.eUserPreference, MemoryPriority.eHigh)
        End Function
        
        ' Store project context
        Public Async Function StoreProjectContext(vProjectPath As String, vContext As String) As Task(Of Boolean)
            Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(vProjectPath)
            Return Await StoreMemory($"project.Context.{lProjectName}", vContext, MemoryCategory.eProjectContext, MemoryPriority.eMedium)
        End Function
        
        ' Store code pattern
        Public Async Function StoreCodePattern(vPattern As String, vDescription As String) As Task(Of Boolean)
            Return Await StoreMemory($"code.Pattern.{vPattern.GetHashCode()}", vDescription, MemoryCategory.eCodePattern, MemoryPriority.eMedium)
        End Function
        
        ' Store error resolution
        Public Async Function StoreErrorResolution(vError As String, vSolution As String) As Task(Of Boolean)
            Return Await StoreMemory($"error.resolution.{vError.GetHashCode()}", vSolution, MemoryCategory.eErrorResolution, MemoryPriority.eHigh)
        End Function
        
        ' Get memory statistics
        Public Function GetMemoryStats() As Dictionary(Of String, Object)
            Try
                Dim lStats As New Dictionary(Of String, Object) From {
                    {"total_memories", pMemoryCache.Count},
                    {"session_memories", pSessionMemories.Count},
                    {"categories", New Dictionary(Of String, Integer)()},
                    {"priorities", New Dictionary(Of String, Integer)()},
                    {"last_update", DateTime.Now}
                }
                
                ' Count by category
                Dim lCategories As Dictionary(Of String, Integer) = CType(lStats("categories"), Dictionary(Of String, Integer))
                For Each lEntry In pMemoryCache.Values
                    Dim lCategory As String = lEntry.Category.ToString()
                    If Not lCategories.ContainsKey(lCategory) Then
                        lCategories(lCategory) = 0
                    End If
                    lCategories(lCategory) += 1
                Next
                
                ' Count by priority
                Dim lPriorities As Dictionary(Of String, Integer) = CType(lStats("priorities"), Dictionary(Of String, Integer))
                For Each lEntry In pMemoryCache.Values
                    Dim lPriority As String = lEntry.Priority.ToString()
                    If Not lPriorities.ContainsKey(lPriority) Then
                        lPriorities(lPriority) = 0
                    End If
                    lPriorities(lPriority) += 1
                Next
                
                Return lStats
                
            Catch ex As Exception
                Console.WriteLine($"GetMemoryStats error: {ex.Message}")
                Return New Dictionary(Of String, Object)()
            End Try
        End Function
        
        ' Clear session memories
        Public Sub ClearSessionMemories()
            Try
                For Each lKey In pSessionMemories
                    If pMemoryCache.ContainsKey(lKey) Then
                        Dim lEntry As MemoryEntry = pMemoryCache(lKey)
                        If Not lEntry.IsPersistent Then
                            pMemoryCache.Remove(lKey)
                        End If
                    End If
                Next
                
                pSessionMemories.Clear()
                
            Catch ex As Exception
                Console.WriteLine($"ClearSessionMemories error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods =====
        
        Private Sub LoadMemoryCache()
            Try
                Dim lCacheFile As String = GetCacheFilePath()
                If File.Exists(lCacheFile) Then
                    Dim lJsonData As String = File.ReadAllText(lCacheFile)
                    If Not String.IsNullOrEmpty(lJsonData) Then
                        Dim lEntries As MemoryEntry() = JsonSerializer.Deserialize(Of MemoryEntry())(lJsonData)
                        
                        For Each lEntry In lEntries
                            pMemoryCache(lEntry.key) = lEntry
                        Next
                        
                        Console.WriteLine($"loaded {lEntries.Length} memories from cache")
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadMemoryCache error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveMemoryCache()
            Try
                Dim lCacheFile As String = GetCacheFilePath()
                Dim lCacheDir As String = System.IO.Path.GetDirectoryName(lCacheFile)
                
                If Not Directory.Exists(lCacheDir) Then
                    Directory.CreateDirectory(lCacheDir)
                End If
                
                Dim lEntries As MemoryEntry() = pMemoryCache.Values.ToArray()
                Dim lJsonData As String = JsonSerializer.Serialize(lEntries, New JsonSerializerOptions With {.WriteIndented = True})
                
                File.WriteAllText(lCacheFile, lJsonData)
                
            Catch ex As Exception
                Console.WriteLine($"SaveMemoryCache error: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetCacheFilePath() As String
            Dim lAppDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            Return System.IO.Path.Combine(lAppDataPath, "SimpleIDE", "memory_cache.json")
        End Function
        
        ' ===== IDisposable Implementation =====
        Private pDisposed As Boolean = False
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
        
        Protected Overridable Sub Dispose(vDisposing As Boolean)
            Try
                If Not pDisposed Then
                    If vDisposing Then
                        ' Save memory cache before disposing
                        SaveMemoryCache()
                        
                        ' Clear session memories
                        ClearSessionMemories()
                        
                        ' Dispose Mem0 client if available
                        If pMem0Client IsNot Nothing Then
                            pMem0Client.Dispose()
                            pMem0Client = Nothing
                        End If
                        
                        pMemoryCache.Clear()
                        pSessionMemories.Clear()
                        pIsInitialized = False
                    End If
                    
                    pDisposed = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MemoryManifest.Dispose error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
