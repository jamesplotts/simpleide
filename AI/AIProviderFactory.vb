' AI/AIProviderFactory.vb - Builds the IAIProvider configured in Preferences' AI tab
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace AI

    ''' <summary>
    ''' Constructs the AI backend the user configured in Preferences, reading provider choice,
    ''' model/generation settings from SettingsManager and API keys securely from
    ''' CredentialManager (never from plain settings)
    ''' </summary>
    Public Class AIProviderFactory

        ''' <summary>
        ''' Builds the currently-configured AI provider
        ''' </summary>
        ''' <param name="vSettingsManager">Settings to read the provider configuration from</param>
        ''' <returns>The configured IAIProvider, or Nothing if no provider is usable yet (e.g. an HTTP provider with no API key set)</returns>
        Public Shared Function CreateProvider(vSettingsManager As SettingsManager) As IAIProvider
            Try
                Dim lProviderName As String = vSettingsManager.GetString("AI.Provider", "ClaudeAPI")
                Dim lModel As String = vSettingsManager.GetString("AI.Model", "")
                Dim lMaxTokens As Integer = vSettingsManager.GetInteger("AI.MaxTokens", 4096)
                Dim lTemperature As Double = vSettingsManager.GetDouble("AI.Temperature", 0.7)
                Dim lCredentialManager As CredentialManager = BuildCredentialManager(vSettingsManager)

                Select Case lProviderName
                    Case "ClaudeCodeCLI"
                        Dim lPath As String = vSettingsManager.GetString("AI.ClaudeCodePath", "claude")
                        Return New ClaudeCodeCliProvider(lPath, lModel)

                    Case "OpenRouter"
                        Dim lApiKey As String = lCredentialManager.RetrieveCredential("SimpleIDE-AI", "OpenRouter")
                        If String.IsNullOrWhiteSpace(lApiKey) Then Return Nothing
                        Dim lBaseUrl As String = vSettingsManager.GetString("AI.BaseUrl", "https://openrouter.ai/api/v1")
                        If String.IsNullOrWhiteSpace(lModel) Then lModel = "anthropic/claude-3.5-sonnet"
                        Return New OpenAICompatibleProvider(lBaseUrl, lApiKey, lModel, lMaxTokens, lTemperature, "OpenRouter")

                    Case "LocalLLM"
                        ' Most local servers (Ollama, LM Studio, etc.) don't require a key at all
                        Dim lApiKey As String = lCredentialManager.RetrieveCredential("SimpleIDE-AI", "LocalLLM")
                        Dim lBaseUrl As String = vSettingsManager.GetString("AI.BaseUrl", "http://localhost:11434/v1")
                        If String.IsNullOrWhiteSpace(lModel) Then Return Nothing
                        Return New OpenAICompatibleProvider(lBaseUrl, lApiKey, lModel, lMaxTokens, lTemperature, "Local LLM")

                    Case Else ' "ClaudeAPI"
                        Dim lApiKey As String = lCredentialManager.RetrieveCredential("SimpleIDE-AI", "ClaudeAPI")
                        If String.IsNullOrWhiteSpace(lApiKey) Then Return Nothing
                        Return New AnthropicProvider(lApiKey, lModel, lMaxTokens, lTemperature)
                End Select

            Catch ex As Exception
                Console.WriteLine($"AIProviderFactory.CreateProvider error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Builds a CredentialManager using whichever secure-storage backend was selected in
        ''' Preferences' Git Credentials tab - that choice is really a machine-level "how do I
        ''' store secrets" preference rather than something Git-specific, so AI credentials
        ''' reuse it instead of asking the user to pick a storage backend a second time
        ''' </summary>
        Private Shared Function BuildCredentialManager(vSettingsManager As SettingsManager) As CredentialManager
            Dim lSavedMethod As String = vSettingsManager.GetString("Git.CredentialStorage", "")
            Dim lMethod As CredentialManager.eStorageMethod
            If Not String.IsNullOrEmpty(lSavedMethod) AndAlso [Enum].TryParse(Of CredentialManager.eStorageMethod)(lSavedMethod, lMethod) Then
                Return New CredentialManager(lMethod)
            End If
            Return New CredentialManager()
        End Function

    End Class

End Namespace
