' Interfaces/IAIProvider.vb - Common contract every AI backend (Claude API, Claude Code CLI,
' OpenRouter, local LLM) implements, so the rest of the IDE can talk to whichever one the user
' configured without caring which it is
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Models

Namespace Interfaces

    ''' <summary>
    ''' A single AI backend the IDE can send prompts to and get text responses from
    ''' </summary>
    Public Interface IAIProvider

        ''' <summary>
        ''' Gets a short, human-readable name for this provider (e.g. "Claude API", "OpenRouter"),
        ''' used in status/error messages so the user knows which backend a failure came from
        ''' </summary>
        ReadOnly Property DisplayName As String

        ''' <summary>
        ''' Sends a prompt (with prior conversation history) to the backend and returns its
        ''' full text response
        ''' </summary>
        ''' <param name="vSystemPrompt">System-level instructions - coding conventions, artifact format, etc.</param>
        ''' <param name="vHistory">Prior turns in the conversation, oldest first</param>
        ''' <param name="vUserMessage">The new user message to send</param>
        ''' <returns>The provider's full text response</returns>
        ''' <exception cref="Exception">Thrown when the request fails - callers show ex.Message to the user</exception>
        Function SendMessageAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String) As Task(Of String)

    End Interface

End Namespace
