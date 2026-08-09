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
                Dim lRequestBody As Object = BuildRequestBody(vSystemPrompt, vHistory, vUserMessage, vStream:=False)
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
        ''' Streams the response via the Messages API's SSE stream (stream: true), invoking
        ''' vOnChunk for each text-delta event as it arrives
        ''' </summary>
        ''' <remarks>
        ''' SSE event shape (https://docs.anthropic.com/en/api/messages-streaming): lines of
        ''' "event: &lt;name&gt;" followed by "data: &lt;json&gt;", blank-line separated. Only
        ''' "content_block_delta" events with delta.type "text_delta" carry response text;
        ''' message_start/content_block_start/message_delta/message_stop/ping are structural
        ''' and ignored here since none of them contain additional text content
        ''' </remarks>
        Public Async Function SendMessageStreamingAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String, vOnChunk As Action(Of String)) As Task(Of String) Implements IAIProvider.SendMessageStreamingAsync
            Try
                Dim lRequestBody As Object = BuildRequestBody(vSystemPrompt, vHistory, vUserMessage, vStream:=True)
                Dim lJson As String = JsonSerializer.Serialize(lRequestBody)

                Dim lRequest As New HttpRequestMessage(HttpMethod.Post, API_URL)
                lRequest.Content = New StringContent(lJson, Encoding.UTF8, "application/json")

                Dim lResponse As HttpResponseMessage = Await pHttpClient.SendAsync(lRequest, HttpCompletionOption.ResponseHeadersRead)

                If Not lResponse.IsSuccessStatusCode Then
                    Dim lErrorText As String = Await lResponse.Content.ReadAsStringAsync()
                    Throw New Exception($"Claude API error {lResponse.StatusCode}: {lErrorText}")
                End If

                Dim lFullText As New StringBuilder()

                Using lStream As System.IO.Stream = Await lResponse.Content.ReadAsStreamAsync()
                    Using lReader As New System.IO.StreamReader(lStream, Encoding.UTF8)
                        Dim lLine As String = Await lReader.ReadLineAsync()
                        While lLine IsNot Nothing
                            If lLine.StartsWith("data:") Then
                                Dim lDataJson As String = lLine.Substring(5).Trim()
                                Dim lChunk As String = ExtractTextDeltaFromEvent(lDataJson)
                                If Not String.IsNullOrEmpty(lChunk) Then
                                    lFullText.Append(lChunk)
                                    vOnChunk(lChunk)
                                End If
                            End If
                            lLine = Await lReader.ReadLineAsync()
                        End While
                    End Using
                End Using

                Return lFullText.ToString()

            Catch ex As Exception
                Throw New Exception($"Claude API error: {ex.Message}", ex)
            End Try
        End Function

        ''' <summary>
        ''' Builds the Messages API request body shared by streaming and non-streaming sends
        ''' </summary>
        Private Function BuildRequestBody(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String, vStream As Boolean) As Object
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

            Return New With {
                .model = pModel,
                .max_tokens = pMaxTokens,
                .messages = lMessages,
                .system = vSystemPrompt,
                .temperature = pTemperature,
                .stream = vStream
            }
        End Function

        ''' <summary>
        ''' Pulls the incremental text out of one SSE "data:" JSON payload, if it's a
        ''' content_block_delta/text_delta event - Nothing/empty for every other event type
        ''' </summary>
        Private Shared Function ExtractTextDeltaFromEvent(vDataJson As String) As String
            Try
                If String.IsNullOrEmpty(vDataJson) OrElse vDataJson = "[DONE]" Then Return ""

                Using lDoc As JsonDocument = JsonDocument.Parse(vDataJson)
                    Dim lRoot As JsonElement = lDoc.RootElement
                    Dim lTypeElement As JsonElement = Nothing
                    If Not lRoot.TryGetProperty("type", lTypeElement) Then Return ""
                    If lTypeElement.GetString() <> "content_block_delta" Then Return ""

                    Dim lDeltaElement As JsonElement = Nothing
                    If Not lRoot.TryGetProperty("delta", lDeltaElement) Then Return ""

                    Dim lDeltaTypeElement As JsonElement = Nothing
                    If Not lDeltaElement.TryGetProperty("type", lDeltaTypeElement) Then Return ""
                    If lDeltaTypeElement.GetString() <> "text_delta" Then Return ""

                    Dim lTextElement As JsonElement = Nothing
                    If Not lDeltaElement.TryGetProperty("text", lTextElement) Then Return ""

                    Return lTextElement.GetString()
                End Using

            Catch ex As Exception
                ' Malformed/partial SSE data line - skip it rather than aborting the whole stream
                Return ""
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
