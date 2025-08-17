' EnhancedClaudeApiClient.vb
' Created: 2025-08-05 17:03:53
' Utilities/EnhancedClaudeApiClient.vb - Enhanced Claude API client with artifact and Mem0 support
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports SimpleIDE.Widgets
Imports SimpleIDE.AI

Namespace Utilities
    
    ''' <summary>
    ''' Enhanced Claude API client supporting artifacts and Mem0 integration
    ''' </summary>
    Public Class EnhancedClaudeApiClient
        
        ' ===== Private Fields =====
        Private ReadOnly pHttpClient As HttpClient
        Private ReadOnly pApiKey As String
        Private pMem0Client As Mem0Client
        Private pUseMem0 As Boolean = False
        Private pProjectContext As String = ""
        Private pUserContext As String = ""
        
        ' ===== Constants =====
        Private Const API_URL As String = "https://api.anthropic.com/v1/messages"
        Private Const MODEL As String = "claude-3-sonnet-20240229"
        Private Const MAX_TOKENS As Integer = 4096
        
        ' ===== Response Classes =====
        Public Class ClaudeResponse
            Public Property Content As String
            Public Property Artifacts As List(Of ClaudeArtifact)
            Public Property StopReason As String
            Public Property Usage As UsageInfo
        End Class
        
        Public Class ClaudeArtifact
            Public Property Id As String
            Public Property Type As String
            Public Property Title As String
            Public Property Content As String
            Public Property Language As String
            Public Property FilePath As String
        End Class
        
        Public Class UsageInfo
            Public Property InputTokens As Integer
            Public Property OutputTokens As Integer
        End Class
        
        ' ===== Constructor =====
        Public Sub New(vApiKey As String, Optional vMem0ApiKey As String = "")
            pApiKey = vApiKey
            pHttpClient = New HttpClient()
            pHttpClient.DefaultRequestHeaders.Add("anthropic-Version", "2023-06-01")
            pHttpClient.DefaultRequestHeaders.Add("x-api-key", pApiKey)
            
            ' Initialize Mem0 if API key provided
            If Not String.IsNullOrEmpty(vMem0ApiKey) Then
                pMem0Client = New Mem0Client(vMem0ApiKey)
                pUseMem0 = True
            End If
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Send message with artifact support
        ''' </summary>
        Public Async Function SendMessageWithArtifactsAsync(vPrompt As String, vHistory As List(Of ImprovedAIAssistantPanel.ChatMessage)) As Task(Of ClaudeResponse)
            Try
                ' Build enhanced prompt with artifact instructions
                Dim lEnhancedPrompt As String = BuildEnhancedPrompt(vPrompt)
                
                ' Add Mem0 context if enabled
                If pUseMem0 Then
                    lEnhancedPrompt = Await AddMem0Context(lEnhancedPrompt)
                End If
                
                ' Build messages array
                Dim lMessages As New List(Of Object)
                
                ' Add conversation history (limited to keep context manageable)
                For Each lMsg In vHistory.TakeLast(10)
                    lMessages.Add(New With {
                        .Role = lMsg.Role,
                        .Content = lMsg.Content
                    })
                Next
                
                ' Add current message
                lMessages.Add(New With {
                    .Role = "user",
                    .Content = lEnhancedPrompt
                })
                
                ' Create request payload
                Dim lPayload As New Dictionary(Of String, Object) From {
                    {"model", MODEL},
                    {"messages", lMessages},
                    {"max_tokens", MAX_TOKENS},
                    {"temperature", 0.7}
                }
                
                ' Send request
                Dim lJson As String = JsonSerializer.Serialize(lPayload)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")
                
                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(API_URL, lContent)
                Dim lResponseText As String = Await lResponse.Content.ReadAsStringAsync()
                
                If Not lResponse.IsSuccessStatusCode Then
                    Throw New Exception($"API error: {lResponse.StatusCode} - {lResponseText}")
                End If
                
                ' Parse response
                Dim lParsedResponse As ClaudeResponse = ParseResponse(lResponseText)
                
                ' Store interaction in Mem0 if enabled
                If pUseMem0 Then
                    Await StoreInteractionInMem0(vPrompt, lParsedResponse)
                End If
                
                Return lParsedResponse
                
            Catch ex As Exception
                Console.WriteLine($"SendMessageWithArtifactsAsync error: {ex.Message}")
                Throw
            End Try
        End Function
        
        ''' <summary>
        ''' Set project context for better responses
        ''' </summary>
        Public Sub SetProjectContext(vProjectInfo As String)
            pProjectContext = vProjectInfo
        End Sub
        
        ''' <summary>
        ''' Set user context from Mem0
        ''' </summary>
        Public Async Function LoadUserContext() As Task
            If pMem0Client IsNot Nothing Then
                Try
                    ' Load user preferences
                    Dim lPreferences As String = Await pMem0Client.RetrieveMemoryAsync("user_preferences")
                    If Not String.IsNullOrEmpty(lPreferences) Then
                        pUserContext &= $"User Preferences: {lPreferences}" & Environment.NewLine
                    End If
                    
                    ' Load recent code patterns
                    Dim lPatterns As List(Of Mem0Client.Memory) = Await pMem0Client.SearchMemoriesAsync("code_pattern", 5)
                    If lPatterns.Count > 0 Then
                        pUserContext &= "Recent code Patterns:" & Environment.NewLine
                        For Each lPattern In lPatterns
                            pUserContext &= $"- {lPattern.key}: {lPattern.Value.Substring(0, Math.Min(100, lPattern.Value.Length))}..." & Environment.NewLine
                        Next
                    End If
                    
                Catch ex As Exception
                    Console.WriteLine($"LoadUserContext error: {ex.Message}")
                End Try
            End If
        End Function
        
        ' ===== Private Helper Methods =====
        
        Private Function BuildEnhancedPrompt(vPrompt As String) As String
            Dim lBuilder As New StringBuilder()
            
            ' Add system instructions for artifact creation
            lBuilder.AppendLine("IMPORTANT: When generating code or substantial Content, create it as an artifact.")
            lBuilder.AppendLine("Format Artifacts as follows:")
            lBuilder.AppendLine("```artifact")
            lBuilder.AppendLine("Id: unique-Id")
            lBuilder.AppendLine("Type: code|documentation|html|react")
            lBuilder.AppendLine("Title: Descriptive Title")
            lBuilder.AppendLine("Language: vb|markdown|html|jsx")
            lBuilder.AppendLine("---")
            lBuilder.AppendLine("(artifact Content here)")
            lBuilder.AppendLine("```")
            lBuilder.AppendLine()
            
            ' Add project context if available
            If Not String.IsNullOrEmpty(pProjectContext) Then
                lBuilder.AppendLine("project Context:")
                lBuilder.AppendLine(pProjectContext)
                lBuilder.AppendLine()
            End If
            
            ' Add user context if available
            If Not String.IsNullOrEmpty(pUserContext) Then
                lBuilder.AppendLine("USER Context (from Memory):")
                lBuilder.AppendLine(pUserContext)
                lBuilder.AppendLine()
            End If
            
            ' Add the actual prompt
            lBuilder.AppendLine("USER REQUEST:")
            lBuilder.AppendLine(vPrompt)
            
            Return lBuilder.ToString()
        End Function
        
        Private Async Function AddMem0Context(vPrompt As String) As Task(Of String)
            If pMem0Client Is Nothing Then Return vPrompt
            
            Try
                Dim lBuilder As New StringBuilder(vPrompt)
                
                ' Search for relevant memories based on prompt
                Dim lRelevantMemories As List(Of Mem0Client.Memory) = Await pMem0Client.SearchMemoriesAsync(vPrompt, 5)
                
                If lRelevantMemories.Count > 0 Then
                    lBuilder.AppendLine()
                    lBuilder.AppendLine("RELEVANT MEMORIES:")
                    For Each lMemory In lRelevantMemories
                        lBuilder.AppendLine($"- {lMemory.key}: {lMemory.Value}")
                    Next
                End If
                
                Return lBuilder.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"AddMem0Context error: {ex.Message}")
                Return vPrompt
            End Try
        End Function
        
        Private Function ParseResponse(vResponseJson As String) As ClaudeResponse
            Try
                Dim lResponse As New ClaudeResponse()
                lResponse.Artifacts = New List(Of ClaudeArtifact)()
                
                ' Parse JSON response
                Dim lDoc As JsonDocument = JsonDocument.Parse(vResponseJson)
                Dim lRoot As JsonElement = lDoc.RootElement
                
                ' Extract content
                If lRoot.TryGetProperty("Content", Nothing) Then
                    Dim lContentArray As JsonElement = lRoot.GetProperty("Content")
                    If lContentArray.GetArrayLength() > 0 Then
                        Dim lFirstContent As JsonElement = lContentArray(0)
                        If lFirstContent.TryGetProperty("Text", Nothing) Then
                            lResponse.Content = lFirstContent.GetProperty("Text").GetString()
                        End If
                    End If
                End If
                
                ' Extract usage info
                If lRoot.TryGetProperty("Usage", Nothing) Then
                    Dim lUsage As JsonElement = lRoot.GetProperty("Usage")
                    lResponse.Usage = New UsageInfo()
                    
                    If lUsage.TryGetProperty("input_tokens", Nothing) Then
                        lResponse.Usage.InputTokens = lUsage.GetProperty("input_tokens").GetInt32()
                    End If
                    
                    If lUsage.TryGetProperty("output_tokens", Nothing) Then
                        lResponse.Usage.OutputTokens = lUsage.GetProperty("output_tokens").GetInt32()
                    End If
                End If
                
                ' Parse artifacts from content
                If Not String.IsNullOrEmpty(lResponse.Content) Then
                    lResponse.Artifacts = ExtractArtifacts(lResponse.Content)
                End If
                
                Return lResponse
                
            Catch ex As Exception
                Console.WriteLine($"ParseResponse error: {ex.Message}")
                Return New ClaudeResponse() With {
                    .Content = vResponseJson,
                    .Artifacts = New List(Of ClaudeArtifact)()
                }
            End Try
        End Function
        
        Private Function ExtractArtifacts(vContent As String) As List(Of ClaudeArtifact)
            Dim lArtifacts As New List(Of ClaudeArtifact)()
            
            Try
                ' Find artifact blocks in the content
                Dim lArtifactPattern As String = "```artifact\s*\n(.*?)```"
                Dim lMatches As System.Text.RegularExpressions.MatchCollection = 
                    System.Text.RegularExpressions.Regex.Matches(vContent, lArtifactPattern, 
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                
                For Each lMatch As System.Text.RegularExpressions.Match In lMatches
                    Dim lArtifactContent As String = lMatch.Groups(1).Value
                    Dim lArtifact As ClaudeArtifact = ParseArtifact(lArtifactContent)
                    If lArtifact IsNot Nothing Then
                        lArtifacts.Add(lArtifact)
                    End If
                Next
                
                ' Also check for standard code blocks that should be artifacts
                If lArtifacts.Count = 0 Then
                    ' Look for VB code blocks
                    Dim lCodePattern As String = "```vb\s*\n(.*?)```"
                    lMatches = System.Text.RegularExpressions.Regex.Matches(vContent, lCodePattern, 
                        System.Text.RegularExpressions.RegexOptions.Singleline)
                    
                    For i As Integer = 0 To lMatches.Count - 1
                        Dim lMatch As System.Text.RegularExpressions.Match = lMatches(i)
                        Dim lCode As String = lMatch.Groups(1).Value.Trim()
                        
                        ' Only create artifact if it's substantial code
                        If lCode.Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries).Length > 5 Then
                            lArtifacts.Add(New ClaudeArtifact() With {
                                .Id = $"code-{Guid.NewGuid().ToString().Substring(0, 8)}",
                                .Type = "code",
                                .Title = ExtractTitleFromCode(lCode),
                                .Content = lCode,
                                .Language = "vb"
                            })
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ExtractArtifacts error: {ex.Message}")
            End Try
            
            Return lArtifacts
        End Function
        
        Private Function ParseArtifact(vArtifactContent As String) As ClaudeArtifact
            Try
                Dim lLines As String() = vArtifactContent.Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                If lLines.Length < 2 Then Return Nothing
                
                Dim lArtifact As New ClaudeArtifact()
                Dim lContentStartIndex As Integer = -1
                
                ' Parse metadata
                For i As Integer = 0 To lLines.Length - 1
                    Dim lLine As String = lLines(i).Trim()
                    
                    If lLine = "---" Then
                        lContentStartIndex = i + 1
                        Exit For
                    End If
                    
                    If lLine.StartsWith("Id:") Then
                        lArtifact.Id = lLine.Substring(3).Trim()
                    ElseIf lLine.StartsWith("Type:") Then
                        lArtifact.Type = lLine.Substring(5).Trim()
                    ElseIf lLine.StartsWith("Title:") Then
                        lArtifact.Title = lLine.Substring(6).Trim()
                    ElseIf lLine.StartsWith("Language:") Then
                        lArtifact.Language = lLine.Substring(9).Trim()
                    ElseIf lLine.StartsWith("FilePath:") Then
                        lArtifact.FilePath = lLine.Substring(9).Trim()
                    End If
                Next
                
                ' Extract content
                If lContentStartIndex >= 0 AndAlso lContentStartIndex < lLines.Length Then
                    Dim lContentLines As New List(Of String)
                    For i As Integer = lContentStartIndex To lLines.Length - 1
                        lContentLines.Add(lLines(i))
                    Next
                    lArtifact.Content = String.Join(Environment.NewLine, lContentLines)
                End If
                
                ' Validate artifact
                If String.IsNullOrEmpty(lArtifact.Id) Then
                    lArtifact.Id = Guid.NewGuid().ToString()
                End If
                
                If String.IsNullOrEmpty(lArtifact.Type) Then
                    lArtifact.Type = "code"
                End If
                
                If String.IsNullOrEmpty(lArtifact.Title) Then
                    lArtifact.Title = "Untitled Artifact"
                End If
                
                Return lArtifact
                
            Catch ex As Exception
                Console.WriteLine($"ParseArtifact error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function ExtractTitleFromCode(vCode As String) As String
            Try
                ' Try to extract a meaningful title from the code
                Dim lLines As String() = vCode.Split({vbCr, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                
                ' Look for class, module, or namespace declarations
                For Each lLine In lLines
                    Dim lTrimmed As String = lLine.Trim()
                    If lTrimmed.StartsWith("Public Class ") OrElse lTrimmed.StartsWith("Class ") Then
                        Return lTrimmed.Replace("Public Class ", "").Replace("Class ", "").Trim()
                    ElseIf lTrimmed.StartsWith("Public Module ") OrElse lTrimmed.StartsWith("Module ") Then
                        Return lTrimmed.Replace("Public Module ", "").Replace("Module ", "").Trim()
                    ElseIf lTrimmed.StartsWith("Namespace ") Then
                        Return lTrimmed.Replace("Namespace ", "").Trim()
                    End If
                Next
                
                ' Look for a comment at the top
                If lLines.Length > 0 AndAlso lLines(0).Trim().StartsWith("'") Then
                    Dim lComment As String = lLines(0).Trim().Substring(1).Trim()
                    If lComment.Length > 0 AndAlso lComment.Length < 50 Then
                        Return lComment
                    End If
                End If
                
                Return "code Snippet"
                
            Catch ex As Exception
                Return "code Snippet"
            End Try
        End Function
        
        Private Async Function StoreInteractionInMem0(vPrompt As String, vResponse As ClaudeResponse) As Task
            If pMem0Client Is Nothing Then Return
            
            Try
                ' Store the interaction
                Dim lInteraction As New Dictionary(Of String, Object) From {
                    {"prompt", vPrompt},
                    {"response", vResponse.Content},
                    {"Timestamp", DateTime.UtcNow.ToString("o")},
                    {"artifacts_count", vResponse.Artifacts.Count}
                }
                
                Await pMem0Client.StoreMemoryAsync(
                    $"interaction_{DateTime.Now.Ticks}", 
                    JsonSerializer.Serialize(lInteraction),
                    New Dictionary(Of String, Object) From {
                        {"Type", "ai_interaction"},
                        {"has_artifacts", vResponse.Artifacts.Count > 0}
                    }
                )
                
                ' Store any code patterns from artifacts
                For Each lArtifact In vResponse.Artifacts
                    If lArtifact.Type = "code" AndAlso Not String.IsNullOrEmpty(lArtifact.Content) Then
                        ' Extract and store code patterns
                        Await StoreCodePattern(lArtifact)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"StoreInteractionInMem0 error: {ex.Message}")
            End Try
        End Function
        
        Private Async Function StoreCodePattern(vArtifact As ClaudeArtifact) As Task
            Try
                ' Extract patterns from the code (simplified example)
                Dim lPatterns As New List(Of String)
                
                ' Look for common patterns
                If vArtifact.Content.Contains("Try") AndAlso vArtifact.Content.Contains("Catch") Then
                    lPatterns.Add("error_handling")
                End If
                
                If vArtifact.Content.Contains("Async Function") Then
                    lPatterns.Add("async_pattern")
                End If
                
                If vArtifact.Content.Contains("AddHandler") Then
                    lPatterns.Add("event_handling")
                End If
                
                ' Store the pattern
                If lPatterns.Count > 0 Then
                    Await pMem0Client.StoreCodeSnippetAsync(
                        vArtifact.Title,
                        vArtifact.Content,
                        vArtifact.Language,
                        $"Patterns: {String.Join(", ", lPatterns)}"
                    )
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StoreCodePattern error: {ex.Message}")
            End Try
        End Function
        
    End Class
    
End Namespace
