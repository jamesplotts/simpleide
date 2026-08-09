' Models/AIChatMessage.vb - Provider-agnostic chat message used when talking to an IAIProvider
Imports System

Namespace Models

    ''' <summary>
    ''' Identifies which AI backend a conversation is configured to use
    ''' </summary>
    Public Enum eAIProviderType
        ''' <summary>No provider configured</summary>
        eUnspecified
        ''' <summary>Anthropic's Claude API (api.anthropic.com), authenticated with an API key</summary>
        eClaudeAPI
        ''' <summary>The locally-installed Claude Code CLI, run in print mode - uses the CLI's own login, no API key needed</summary>
        eClaudeCodeCLI
        ''' <summary>OpenRouter (openrouter.ai) - an OpenAI-compatible endpoint proxying many models</summary>
        eOpenRouter
        ''' <summary>A local LLM server exposing an OpenAI-compatible endpoint (Ollama, LM Studio, text-generation-webui, etc.)</summary>
        eLocalLLM
        ''' <summary>Sentinel value for enum bounds checking</summary>
        eLastValue
    End Enum

    ''' <summary>
    ''' A single turn in a conversation with an AI provider - deliberately minimal (just role
    ''' and text) so every IAIProvider implementation can consume it regardless of which
    ''' vendor-specific message shape it has to translate it into
    ''' </summary>
    Public Class AIChatMessage

        ''' <summary>Gets or sets who sent this message - "user" or "assistant"</summary>
        Public Property Role As String

        ''' <summary>Gets or sets the message text</summary>
        Public Property Content As String

        ''' <summary>
        ''' Creates a new chat message
        ''' </summary>
        ''' <param name="vRole">"user" or "assistant"</param>
        ''' <param name="vContent">The message text</param>
        Public Sub New(vRole As String, vContent As String)
            Role = vRole
            Content = vContent
        End Sub

    End Class

End Namespace
