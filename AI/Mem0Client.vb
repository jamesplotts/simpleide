' AI/Mem0Client.vb - Mem0 API client for persistent memory management
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks

Namespace AI
    
    Public Class Mem0Client
        
        Private ReadOnly pHttpClient As HttpClient
        Private ReadOnly pApiKey As String
        Private Const API_BASE_URL As String = "https://api.mem0.ai/v1"
        
        ' Memory structure
        Public Class Memory
            Public Property Id As String
            Public Property key As String
            Public Property Value As String
            Public Property Metadata As Dictionary(Of String, Object)
            Public Property CreatedAt As DateTime
            Public Property UpdatedAt As DateTime
            Public Property UserId As String
            Public Property AppId As String
        End Class
        
        ' Search result
        Public Class SearchResult
            Public Property Memory As Memory
            Public Property Score As Double
        End Class
        
        ' Constructor
        Public Sub New(vApiKey As String)
            pApiKey = vApiKey
            pHttpClient = New HttpClient()
            pHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {pApiKey}")
            pHttpClient.DefaultRequestHeaders.Add("Content-Type", "application/json")
        End Sub
        
        ' Store a memory
        Public Async Function StoreMemoryAsync(vKey As String, vValue As String, Optional vMetadata As Dictionary(Of String, Object) = Nothing) As Task(Of Boolean)
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories"
                
                Dim lPayload As New Dictionary(Of String, Object) From {
                    {"key", vKey},
                    {"Value", vValue}
                }
                
                If vMetadata IsNot Nothing Then
                    lPayload.Add("Metadata", vMetadata)
                End If
                
                Dim lJson As String = JsonSerializer.Serialize(lPayload)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(lUrl, lContent)
                
                Return lResponse.IsSuccessStatusCode
                
            Catch ex As Exception
                Console.WriteLine($"StoreMemoryAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Retrieve a memory by key
        Public Async Function RetrieveMemoryAsync(vKey As String) As Task(Of String)
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories/key/{Uri.EscapeDataString(vKey)}"
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.GetAsync(lUrl)
                
                If lResponse.IsSuccessStatusCode Then
                    Dim lJson As String = Await lResponse.Content.ReadAsStringAsync()
                    Dim lMemory As JsonDocument = JsonDocument.Parse(lJson)
                    
                    ' FIXED: Declare lValue before using it
                    Dim lValue As JsonElement = Nothing
                    If lMemory.RootElement.TryGetProperty("Value", lValue) Then
                        Return lValue.GetString()
                    End If
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"RetrieveMemoryAsync error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Search memories
        Public Async Function SearchMemoriesAsync(vQuery As String, Optional vLimit As Integer = 10) As Task(Of List(Of Memory))
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories/search"
                
                Dim lPayload As New Dictionary(Of String, Object) From {
                    {"query", vQuery},
                    {"limit", vLimit}
                }
                
                Dim lJson As String = JsonSerializer.Serialize(lPayload)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(lUrl, lContent)
                
                If lResponse.IsSuccessStatusCode Then
                    Dim lResponseJson As String = Await lResponse.Content.ReadAsStringAsync()
                    Dim lResults As List(Of SearchResult) = JsonSerializer.Deserialize(Of List(Of SearchResult))(lResponseJson)
                    
                    ' Extract memories from search results
                    Dim lMemories As New List(Of Memory)
                    If lResults IsNot Nothing Then
                        For Each lResult In lResults
                            If lResult.Memory IsNot Nothing Then
                                lMemories.Add(lResult.Memory)
                            End If
                        Next
                    End If
                    
                    Return lMemories
                End If
                
                Return New List(Of Memory)
                
            Catch ex As Exception
                Console.WriteLine($"SearchMemoriesAsync error: {ex.Message}")
                Return New List(Of Memory)
            End Try
        End Function
        
        ' Update a memory
        Public Async Function UpdateMemoryAsync(vId As String, vValue As String, Optional vMetadata As Dictionary(Of String, Object) = Nothing) As Task(Of Boolean)
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories/{vId}"
                
                Dim lPayload As New Dictionary(Of String, Object) From {
                    {"Value", vValue}
                }
                
                If vMetadata IsNot Nothing Then
                    lPayload.Add("Metadata", vMetadata)
                End If
                
                Dim lJson As String = JsonSerializer.Serialize(lPayload)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.PutAsync(lUrl, lContent)
                
                Return lResponse.IsSuccessStatusCode
                
            Catch ex As Exception
                Console.WriteLine($"UpdateMemoryAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Delete a memory
        Public Async Function DeleteMemoryAsync(vId As String) As Task(Of Boolean)
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories/{vId}"
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.DeleteAsync(lUrl)
                
                Return lResponse.IsSuccessStatusCode
                
            Catch ex As Exception
                Console.WriteLine($"DeleteMemoryAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Store code snippet
        Public Async Function StoreCodeSnippetAsync(vName As String, vCode As String, vLanguage As String, vDescription As String) As Task(Of Boolean)
            Try
                Dim lMetadata As New Dictionary(Of String, Object) From {
                    {"Type", "code_snippet"},
                    {"Language", vLanguage},
                    {"Description", vDescription},
                    {"Timestamp", DateTime.UtcNow.ToString("o")}
                }
                
                Return Await StoreMemoryAsync($"snippet_{vName}", vCode, lMetadata)
                
            Catch ex As Exception
                Console.WriteLine($"StoreCodeSnippetAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Store project context
        Public Async Function StoreProjectContextAsync(vProjectName As String, vContext As Dictionary(Of String, Object)) As Task(Of Boolean)
            Try
                Dim lMetadata As New Dictionary(Of String, Object) From {
                    {"Type", "project_context"},
                    {"Timestamp", DateTime.UtcNow.ToString("o")}
                }
                
                ' Merge context into metadata
                For Each lKvp In vContext
                    lMetadata(lKvp.key) = lKvp.Value
                Next
                
                Dim lValue As String = JsonSerializer.Serialize(vContext)
                
                Return Await StoreMemoryAsync($"project_{vProjectName}", lValue, lMetadata)
                
            Catch ex As Exception
                Console.WriteLine($"StoreProjectContextAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get all memories for a user
        Public Async Function GetUserMemoriesAsync(Optional vUserId As String = Nothing, Optional vLimit As Integer = 100) As Task(Of List(Of Memory))
            Try
                Dim lUrl As String = $"{API_BASE_URL}/memories"
                If Not String.IsNullOrEmpty(vUserId) Then
                    lUrl &= $"?user_id={Uri.EscapeDataString(vUserId)}"
                End If
                lUrl &= $"&limit={vLimit}"
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.GetAsync(lUrl)
                
                If lResponse.IsSuccessStatusCode Then
                    Dim lJson As String = Await lResponse.Content.ReadAsStringAsync()
                    Return JsonSerializer.Deserialize(Of List(Of Memory))(lJson)
                End If
                
                Return New List(Of Memory)
                
            Catch ex As Exception
                Console.WriteLine($"GetUserMemoriesAsync error: {ex.Message}")
                Return New List(Of Memory)
            End Try
        End Function
        
        ' Batch store memories
        Public Async Function BatchStoreMemoriesAsync(vMemories As List(Of Tuple(Of String, String, Dictionary(Of String, Object)))) As Task(Of Boolean)
            Try
                Dim lSuccessCount As Integer = 0
                
                For Each lMemory In vMemories
                    Dim lResult As Boolean = Await StoreMemoryAsync(lMemory.Item1, lMemory.Item2, lMemory.Item3)
                    If lResult Then lSuccessCount += 1
                Next
                
                Return lSuccessCount = vMemories.Count
                
            Catch ex As Exception
                Console.WriteLine($"BatchStoreMemoriesAsync error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Dispose
        Public Sub Dispose()
            pHttpClient?.Dispose()
        End Sub
        
    End Class
    
End Namespace
