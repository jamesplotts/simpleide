' AI/ClaudeCodeCliProvider.vb - IAIProvider implementation that shells out to the locally
' installed Claude Code CLI in print mode, instead of talking to any API directly. Uses
' whatever the CLI is already logged into on this machine - no API key stored or sent by
' SimpleIDE at all.
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Text
Imports System.Threading.Tasks
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models

Namespace AI

    ''' <summary>
    ''' Runs the "claude" CLI (claude.ai/code) in non-interactive print mode for a single
    ''' request/response turn
    ''' </summary>
    ''' <remarks>
    ''' The CLI's own session/context management (--continue, --resume) isn't used here - each
    ''' call is a fresh, independent invocation, so the full conversation history is flattened
    ''' into the prompt text sent on every call. This keeps the implementation simple at the
    ''' cost of the CLI not seeing this as one ongoing session; it works well enough for the
    ''' IDE's chat panel, which already resends its own history to every provider anyway.
    ''' </remarks>
    Public Class ClaudeCodeCliProvider
        Implements IAIProvider

        Private ReadOnly pExecutablePath As String
        Private ReadOnly pModel As String
        Private ReadOnly pWorkingDirectory As String

        ''' <summary>
        ''' Creates a provider that shells out to the Claude Code CLI
        ''' </summary>
        ''' <param name="vExecutablePath">Path to the "claude" executable, or just "claude" to resolve it via PATH</param>
        ''' <param name="vModel">Optional model override passed as --model, or empty to use the CLI's own default</param>
        ''' <param name="vWorkingDirectory">Directory to run the CLI in, so it can see the current project - empty uses the IDE's own working directory</param>
        Public Sub New(Optional vExecutablePath As String = "claude", Optional vModel As String = "", Optional vWorkingDirectory As String = "")
            pExecutablePath = If(String.IsNullOrWhiteSpace(vExecutablePath), "claude", vExecutablePath)
            pModel = vModel
            pWorkingDirectory = vWorkingDirectory
        End Sub

        Public ReadOnly Property DisplayName As String Implements IAIProvider.DisplayName
            Get
                Return "Claude Code CLI"
            End Get
        End Property

        Public Async Function SendMessageAsync(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String) As Task(Of String) Implements IAIProvider.SendMessageAsync
            Try
                Dim lPrompt As String = BuildFlattenedPrompt(vSystemPrompt, vHistory, vUserMessage)

                Dim lStartInfo As New ProcessStartInfo()
                lStartInfo.FileName = pExecutablePath
                lStartInfo.ArgumentList.Add("-p")
                lStartInfo.ArgumentList.Add(lPrompt)
                lStartInfo.ArgumentList.Add("--output-format")
                lStartInfo.ArgumentList.Add("text")
                If Not String.IsNullOrWhiteSpace(pModel) Then
                    lStartInfo.ArgumentList.Add("--model")
                    lStartInfo.ArgumentList.Add(pModel)
                End If
                If Not String.IsNullOrEmpty(pWorkingDirectory) Then
                    lStartInfo.WorkingDirectory = pWorkingDirectory
                End If
                lStartInfo.UseShellExecute = False
                lStartInfo.RedirectStandardOutput = True
                lStartInfo.RedirectStandardError = True
                lStartInfo.CreateNoWindow = True

                Using lProcess As New Process()
                    lProcess.StartInfo = lStartInfo

                    Try
                        lProcess.Start()
                    Catch ex As Exception
                        Throw New Exception($"Could not start the Claude Code CLI ('{pExecutablePath}') - is it installed and on PATH? {ex.Message}", ex)
                    End Try

                    Dim lOutputTask As Task(Of String) = lProcess.StandardOutput.ReadToEndAsync()
                    Dim lErrorTask As Task(Of String) = lProcess.StandardError.ReadToEndAsync()

                    Await Task.Run(Sub() lProcess.WaitForExit())
                    Dim lOutput As String = Await lOutputTask
                    Dim lError As String = Await lErrorTask

                    If lProcess.ExitCode <> 0 Then
                        Throw New Exception($"Claude Code CLI exited with code {lProcess.ExitCode}: {If(String.IsNullOrWhiteSpace(lError), lOutput, lError)}")
                    End If

                    Return lOutput.Trim()
                End Using

            Catch ex As Exception
                Throw New Exception($"Claude Code CLI error: {ex.Message}", ex)
            End Try
        End Function

        ''' <summary>
        ''' Flattens the system prompt, prior turns, and the new message into a single prompt
        ''' string, since each CLI invocation here is an independent process with no memory of
        ''' earlier calls
        ''' </summary>
        Private Shared Function BuildFlattenedPrompt(vSystemPrompt As String, vHistory As List(Of AIChatMessage), vUserMessage As String) As String
            Dim lBuilder As New StringBuilder()

            If Not String.IsNullOrEmpty(vSystemPrompt) Then
                lBuilder.AppendLine(vSystemPrompt)
                lBuilder.AppendLine()
            End If

            If vHistory IsNot Nothing AndAlso vHistory.Count > 0 Then
                lBuilder.AppendLine("Conversation so far:")
                for each lMsg in vHistory
                    lBuilder.AppendLine($"{lMsg.Role}: {lMsg.Content}")
                Next
                lBuilder.AppendLine()
            End If

            lBuilder.Append(vUserMessage)

            Return lBuilder.ToString()
        End Function

    End Class

End Namespace
