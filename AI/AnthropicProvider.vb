' AI/AnthropicProvider.vb - IAIProvider implementation for Anthropic's Claude API
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace AI

    ''' <summary>
    ''' Talks to Anthropic's Messages API (api.anthropic.com) directly with an API key
    ''' </summary>
    Public Class AnthropicProvider
        Implements IAIProvider

        Private Const API_URL As String = "https://api.anthropic.com/v1/messages"
        Private Const DEFAULT_MODEL As String = "claude-sonnet-4-5"

        Private ReadOnly pHttpClient As HttpClient
        Private ReadOnly pModel As String
        Private ReadOnly pMaxTokens As Integer
        Private ReadOnly pTemperature As Double

        ''' <summary>
        ''' Creates a provider that talks to the Claude API
        ''' </summary>
        ''' <param name="vApiKey">Anthropic API key</param>
        ''' <param name="vModel">Model ID to request, e.g. "claude-sonnet-4-5" - falls back to a current default if empty</param>
        ''' <param name="vMaxTokens">Maximum tokens in the response</param>
        ''' <param name="vTemperature">Sampling temperature, 0.0-1.0</param>
        Public Sub New(vApiKey As String, Optional vModel As String = "", Optional vMaxTokens As Integer = 4096, Optional vTemperature As Double = 0.7)
            pHttpClient = New HttpClient()
            pHttpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01")
            pHttpClient.DefaultRequestHeaders.Add("x-api-key", vApiKey)
            pModel = If(String.IsNullOrWhiteSpace(vModel), DEFAULT_MODEL, vModel)
            pMaxTokens = vMaxTokens
            pTemperature = vTemperature
        End Sub

        Public ReadOnly Property DisplayName As String Implements IAIProvider.DisplayName
            Get
                Return "Claude API"
            End Get
        End Property

        Public Async Function SendMessageAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String) As Task(Of String) Implements IAIProvider.SendMessageAsync
            Try
                Dim lMessages As New List(Of Object)

                for each lMsg in vHistory
                    lMessages.Add(New With {
                        .role = lMsg.Role,
                        .content = lMsg.Content
                    })
                Next

                lMessages.Add(New With {
                    .role = "user",
                    .content = vUserMessage
                })

                Dim lRequestBody As New With {
                    .model = pModel,
                    .max_tokens = pMaxTokens,
                    .messages = lMessages,
                    .system = vSystemPrompt,
                    .temperature = pTemperature
                }

                Dim lJson As String = JsonSerializer.Serialize(lRequestBody)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")

                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(API_URL, lContent)
                Dim lResponseText As String = Await lResponse.Content.ReadAsStringAsync()

                If Not lResponse.IsSuccessStatusCode Then
                    Throw New Exception($"Claude API error {lResponse.StatusCode}: {lResponseText}")
                End If

                Return ExtractResponseText(lResponseText)

            Catch ex As Exception
                Throw New Exception($"Claude API error: {ex.Message}", ex)
            End Try
        End Function

        ''' <summary>
        ''' Pulls the assistant's reply text out of a Messages API response body
        ''' </summary>
        ''' <param name="vResponseJson">Raw JSON response from the API</param>
        ''' <returns>The concatenated text of every text content block</returns>
        ''' <remarks>
        ''' Uses case-insensitive property lookup deliberately - the API returns lowercase
        ''' JSON keys ("content", "text"), and System.Text.Json's JsonElement.TryGetProperty
        ''' is case-sensitive by default, so a prior version of this parsing logic that queried
        ''' "Content"/"Text" (PascalCase) never matched anything and always returned empty
        ''' </remarks>
        Private Shared Function ExtractResponseText(vResponseJson As String) As String
            Using lDoc As JsonDocument = JsonDocument.Parse(vResponseJson)
                Dim lRoot As JsonElement = lDoc.RootElement
                Dim lContentElement As JsonElement = Nothing
                If Not lRoot.TryGetProperty("content", lContentElement) Then Return ""
                If lContentElement.ValueKind <> JsonValueKind.Array Then Return ""

                Dim lBuilder As New StringBuilder()
                for each lBlock in lContentElement.EnumerateArray()
                    Dim lTypeElement As JsonElement = Nothing
                    Dim lTextElement As JsonElement = Nothing
                    If lBlock.TryGetProperty("type", lTypeElement) AndAlso lTypeElement.GetString() = "text" Then
                        If lBlock.TryGetProperty("text", lTextElement) Then
                            lBuilder.Append(lTextElement.GetString())
                        End If
                    End If
                Next

                Return lBuilder.ToString()
            End Using
        End Function

        Public Sub Dispose()
            pHttpClient?.Dispose()
        End Sub

    End Class

End Namespace
