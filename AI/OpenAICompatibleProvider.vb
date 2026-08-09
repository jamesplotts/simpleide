' AI/OpenAICompatibleProvider.vb - IAIProvider implementation for any server speaking the OpenAI
' Chat Completions schema. That covers OpenRouter, and effectively every local LLM server
' (Ollama, LM Studio, text-generation-webui, llama.cpp's server, vLLM, etc.) since they all
' expose the same /chat/completions endpoint shape - so one client serves all of them, the only
' difference being which BaseUrl/ApiKey/Model the user configures
Imports System
Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace AI

    ''' <summary>
    ''' Talks to any OpenAI-compatible /chat/completions endpoint - OpenRouter, or a local LLM
    ''' server such as Ollama or LM Studio
    ''' </summary>
    Public Class OpenAICompatibleProvider
        Implements IAIProvider

        Private ReadOnly pHttpClient As HttpClient
        Private ReadOnly pEndpoint As String
        Private ReadOnly pModel As String
        Private ReadOnly pMaxTokens As Integer
        Private ReadOnly pTemperature As Double
        Private ReadOnly pDisplayName As String

        ''' <summary>
        ''' Creates a provider that talks to an OpenAI-compatible chat completions endpoint
        ''' </summary>
        ''' <param name="vBaseUrl">Server base URL, e.g. "https://openrouter.ai/api/v1" or "http://localhost:11434/v1" - "/chat/completions" is appended automatically</param>
        ''' <param name="vApiKey">Bearer token to send, or empty for servers that don't require one (typical for local LLMs)</param>
        ''' <param name="vModel">Model ID to request, e.g. "anthropic/claude-3.5-sonnet" (OpenRouter) or "llama3:8b" (Ollama)</param>
        ''' <param name="vMaxTokens">Maximum tokens in the response</param>
        ''' <param name="vTemperature">Sampling temperature, 0.0-1.0</param>
        ''' <param name="vDisplayName">Human-readable name shown in status/error messages, e.g. "OpenRouter" or "Local LLM"</param>
        Public Sub New(vBaseUrl As String, vApiKey As String, vModel As String, Optional vMaxTokens As Integer = 4096, Optional vTemperature As Double = 0.7, Optional vDisplayName As String = "OpenAI-compatible")
            pHttpClient = New HttpClient()
            If Not String.IsNullOrWhiteSpace(vApiKey) Then
                pHttpClient.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", vApiKey)
            End If
            pEndpoint = vBaseUrl.TrimEnd("/"c) & "/chat/completions"
            pModel = vModel
            pMaxTokens = vMaxTokens
            pTemperature = vTemperature
            pDisplayName = vDisplayName
        End Sub

        Public ReadOnly Property DisplayName As String Implements IAIProvider.DisplayName
            Get
                Return pDisplayName
            End Get
        End Property

        Public Async Function SendMessageAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String) As Task(Of String) Implements IAIProvider.SendMessageAsync
            Try
                Dim lRequestBody As Object = BuildRequestBody(vSystemPrompt, vHistory, vUserMessage, vStream:=False)
                Dim lJson As String = JsonSerializer.Serialize(lRequestBody)
                Dim lContent As New StringContent(lJson, Encoding.UTF8, "application/json")

                Dim lResponse As HttpResponseMessage = Await pHttpClient.PostAsync(pEndpoint, lContent)
                Dim lResponseText As String = Await lResponse.Content.ReadAsStringAsync()

                If Not lResponse.IsSuccessStatusCode Then
                    Throw New Exception($"{pDisplayName} error {lResponse.StatusCode}: {lResponseText}")
                End If

                Return ExtractResponseText(lResponseText)

            Catch ex As Exception
                Throw New Exception($"{pDisplayName} error: {ex.Message}", ex)
            End Try
        End Function

        ''' <summary>
        ''' Streams the response via the Chat Completions endpoint's SSE stream (stream: true),
        ''' invoking vOnChunk for each delta as it arrives
        ''' </summary>
        ''' <remarks>
        ''' SSE event shape (OpenAI Chat Completions streaming, also followed by OpenRouter and
        ''' every local server tested - Ollama's /v1 shim, LM Studio, text-generation-webui,
        ''' llama.cpp server, vLLM): "data: &lt;json&gt;" lines, each with
        ''' choices[0].delta.content holding the next text fragment (absent/null on the final
        ''' role-only chunk), terminated by a literal "data: [DONE]" line
        ''' </remarks>
        Public Async Function SendMessageStreamingAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String, vOnChunk As Action(Of String)) As Task(Of String) Implements IAIProvider.SendMessageStreamingAsync
            Try
                Dim lRequestBody As Object = BuildRequestBody(vSystemPrompt, vHistory, vUserMessage, vStream:=True)
                Dim lJson As String = JsonSerializer.Serialize(lRequestBody)

                Dim lRequest As New HttpRequestMessage(HttpMethod.Post, pEndpoint)
                lRequest.Content = New StringContent(lJson, Encoding.UTF8, "application/json")

                Dim lResponse As HttpResponseMessage = Await pHttpClient.SendAsync(lRequest, HttpCompletionOption.ResponseHeadersRead)

                If Not lResponse.IsSuccessStatusCode Then
                    Dim lErrorText As String = Await lResponse.Content.ReadAsStringAsync()
                    Throw New Exception($"{pDisplayName} error {lResponse.StatusCode}: {lErrorText}")
                End If

                Dim lFullText As New StringBuilder()

                Using lStream As System.IO.Stream = Await lResponse.Content.ReadAsStreamAsync()
                    Using lReader As New System.IO.StreamReader(lStream, Encoding.UTF8)
                        Dim lLine As String = Await lReader.ReadLineAsync()
                        While lLine IsNot Nothing
                            If lLine.StartsWith("data:") Then
                                Dim lDataJson As String = lLine.Substring(5).Trim()
                                If lDataJson = "[DONE]" Then Exit While
                                Dim lChunk As String = ExtractDeltaContent(lDataJson)
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
                Throw New Exception($"{pDisplayName} error: {ex.Message}", ex)
            End Try
        End Function

        ''' <summary>
        ''' Builds the Chat Completions request body shared by streaming and non-streaming sends
        ''' </summary>
        Private Function BuildRequestBody(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String, vStream As Boolean) As Object
            Dim lMessages As New List(Of Object)

            If Not String.IsNullOrEmpty(vSystemPrompt) Then
                lMessages.Add(New With {.role = "system", .content = vSystemPrompt})
            End If

            for each lMsg in vHistory
                lMessages.Add(New With {
                    .role = lMsg.Role,
                    .content = lMsg.Content
                })
            Next

            lMessages.Add(New With {.role = "user", .content = vUserMessage})

            Return New With {
                .model = pModel,
                .messages = lMessages,
                .max_tokens = pMaxTokens,
                .temperature = pTemperature,
                .stream = vStream
            }
        End Function

        ''' <summary>
        ''' Pulls choices[0].delta.content out of one SSE "data:" JSON payload - Nothing/empty
        ''' if this chunk carries no text (e.g. the role-only first chunk, or a finish-reason-only
        ''' last chunk)
        ''' </summary>
        Private Shared Function ExtractDeltaContent(vDataJson As String) As String
            Try
                If String.IsNullOrEmpty(vDataJson) Then Return ""

                Using lDoc As JsonDocument = JsonDocument.Parse(vDataJson)
                    Dim lRoot As JsonElement = lDoc.RootElement
                    Dim lChoicesElement As JsonElement = Nothing
                    If Not lRoot.TryGetProperty("choices", lChoicesElement) Then Return ""
                    If lChoicesElement.ValueKind <> JsonValueKind.Array OrElse lChoicesElement.GetArrayLength() = 0 Then Return ""

                    Dim lFirstChoice As JsonElement = lChoicesElement(0)
                    Dim lDeltaElement As JsonElement = Nothing
                    If Not lFirstChoice.TryGetProperty("delta", lDeltaElement) Then Return ""

                    Dim lContentElement As JsonElement = Nothing
                    If Not lDeltaElement.TryGetProperty("content", lContentElement) Then Return ""
                    If lContentElement.ValueKind <> JsonValueKind.String Then Return ""

                    Return lContentElement.GetString()
                End Using

            Catch ex As Exception
                ' Malformed/partial SSE data line - skip it rather than aborting the whole stream
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Pulls the assistant's reply text out of a Chat Completions response body
        ''' (choices[0].message.content)
        ''' </summary>
        Private Shared Function ExtractResponseText(vResponseJson As String) As String
            Using lDoc As JsonDocument = JsonDocument.Parse(vResponseJson)
                Dim lRoot As JsonElement = lDoc.RootElement
                Dim lChoicesElement As JsonElement = Nothing
                If Not lRoot.TryGetProperty("choices", lChoicesElement) Then Return ""
                If lChoicesElement.ValueKind <> JsonValueKind.Array OrElse lChoicesElement.GetArrayLength() = 0 Then Return ""

                Dim lFirstChoice As JsonElement = lChoicesElement(0)
                Dim lMessageElement As JsonElement = Nothing
                If Not lFirstChoice.TryGetProperty("message", lMessageElement) Then Return ""

                Dim lContentElement As JsonElement = Nothing
                If Not lMessageElement.TryGetProperty("content", lContentElement) Then Return ""

                Return lContentElement.GetString()
            End Using
        End Function

        Public Sub Dispose()
            pHttpClient?.Dispose()
        End Sub

    End Class

End Namespace
