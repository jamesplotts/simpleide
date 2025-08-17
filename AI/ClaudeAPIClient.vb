' Utilities/ClaudeApiClient.vb - Claude API client for AI integration
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports SimpleIDE.Widgets

Namespace Utilities
    Public Class ClaudeApiClient
        Private ReadOnly pHttpClient As HttpClient
        Private ReadOnly pApiKey As String
        Private Const API_URL As String = "https://api.anthropic.com/v1/messages"
        Private Const MODEL As String = "claude-3-sonnet-20240229" ' Or claude-3-opus-20240229
        Private Const MAX_TOKENS As Integer = 4096
        
        Public Sub New(vApiKey As String)
            pApiKey = vApiKey
            pHttpClient = New HttpClient()
            pHttpClient.DefaultRequestHeaders.Add("anthropic-Version", "2023-06-01")
            pHttpClient.DefaultRequestHeaders.Add("x-api-key", pApiKey)
        End Sub
        
        Public Async Function SendMessageAsync(vPrompt As String, vHistory As List(Of AIAssistantPanel.ChatMessage)) As Task(Of String)
            Try
                ' Build messages array
                Dim lMessages As New List(Of Object)
                
                ' Add conversation history
                For Each lMsg In vHistory.Take(10) ' Limit history to keep Context window manageable
                    lMessages.Add(New With {
                        .Role = lMsg.Role,
                        .Content = lMsg.Content
                    })
                Next
                
                ' Add current prompt
                lMessages.Add(New With {
                    .Role = "user",
                    .Content = vPrompt
                })
                
                ' Add system prompt
                Dim lSystemPrompt As String = GetSystemPrompt()
                
                ' Create request body
                Dim lRequestBody As New With {
                    .model = MODEL,
                    .max_tokens = MAX_TOKENS,
                    .messages = lMessages,
                    .system = lSystemPrompt,
                    .temperature = 0.7
                }
                
                ' Serialize to JSON
                Dim lJsonOptions As New JsonSerializerOptions() With {
                    .PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }
                Dim lJson As String = JsonSerializer.Serialize(lRequestBody, lJsonOptions)
                
                ' Create request
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")
                
                ' Send request
                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(API_URL, lContent)
                
                If Not lResponse.IsSuccessStatusCode Then
                    Dim lError As String = Await lResponse.Content.ReadAsStringAsync()
                    Throw New Exception($"API error {lResponse.StatusCode}: {lError}")
                End If
                
                ' Parse response
                Dim lResponseJson As String = Await lResponse.Content.ReadAsStringAsync()
                Dim lResponseObj As JsonDocument = JsonDocument.Parse(lResponseJson)
                
                ' Extract content - DECLARE VARIABLES
                Dim lContentValue As String = ""
                Dim lRootElement As JsonElement = lResponseObj.RootElement
                Dim lContentElement As JsonElement = Nothing
                Dim lTextElement As JsonElement = Nothing
                
                ' Fixed parsing logic
                If lRootElement.TryGetProperty("Content", lContentElement) Then
                    If lContentElement.ValueKind = JsonValueKind.Array AndAlso lContentElement.GetArrayLength() > 0 Then
                        Dim lFirstContent = lContentElement.Item(0)
                        If lFirstContent.TryGetProperty("Text", lTextElement) Then
                            lContentValue = lTextElement.GetString()
                        End If
                    End If
                End If
                
                Return If(String.IsNullOrEmpty(lContentValue), "No response Content", lContentValue)
                
            Catch ex As Exception
                Throw New Exception($"Claude API error: {ex.Message}", ex)
            End Try
        End Function
        
        Private Function GetSystemPrompt() As String
            Return "You are an AI coding assistant integrated into SimpleIDE, a VB.NET development environment. " & _
                   "You help users write VB.NET code following these strict conventions:" & Environment.NewLine & _
                   Environment.NewLine & _
                   "CODING CONVENTIONS (MUST FOLLOW):" & Environment.NewLine & _
                   "1. Hungarian Notation: l=Local, p=Private, v=Parameter, g=Global" & Environment.NewLine & _
                   "2. Enums: Start with eUnspecified, end with eLastValue, Prefix values with 'e'" & Environment.NewLine & _
                   "3. Methods: PascalCase, Events: On[Event] Pattern" & Environment.NewLine & _
                   "4. GTK# specific: Use System.IO.Path fully qualified, Environment.NewLine not vbNewLine" & Environment.NewLine & _
                   "5. Always use Try-Catch blocks with Console.WriteLine for debugging" & Environment.NewLine & _
                   "6. Comments: Use ' TODO:, ' FIXED:, ' NOTE: prefixes" & Environment.NewLine & _
                   Environment.NewLine & _
                   "When creating or modifying code:" & Environment.NewLine & _
                   "- Follow the existing project structure and patterns" & Environment.NewLine & _
                   "- Use partial classes for large forms (MainWindow.*.vb Pattern)" & Environment.NewLine & _
                   "- Implement comprehensive error handling" & Environment.NewLine & _
                   "- Use the existing CssHelper utility for styling" & Environment.NewLine & _
                   "- Maintain event-driven architecture" & Environment.NewLine & _
                   Environment.NewLine & _
                   "You can create files, modify existing code, explain code, fix Errors, and help with refactoring. " & _
                   "Always provide Clear explanations of what you're doing and why."
        End Function
        
        Public Sub Dispose()
            pHttpClient?.Dispose()
        End Sub
    End Class
End Namespace
 
