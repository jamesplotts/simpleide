' Widgets/ChatHistoryMessage.vb - Conversation-history element types shared by
' AIAssistantPanel and AIChatClient. Extracted from the former
' ImprovedAIAssistantPanel.vb, which was entirely dead code (never instantiated anywhere -
' only these two nested classes were actually live, reused by AIAssistantPanel/AIChatClient
' under the ImprovedAIAssistantPanel.ChatMessage/ArtifactInfo names) - the rest of that
' class has been removed. Named ChatHistoryMessage rather than plain ChatMessage or
' AIChatMessage since both those names are already taken by other, differently-shaped
' classes in this codebase: AIAssistantPanel had its own now-removed dead nested
' ChatMessage (Role/Content/Actions, no Artifacts), and Models.AIChatMessage is the
' provider-agnostic Role/Content pair IAIProvider implementations consume - this one is
' neither; it's specifically the artifact/action-carrying history entry
' SendMessageWithArtifactsAsync's vHistory parameter and AIAssistantPanel's own
' pConversationHistory use
Imports System
Imports System.Collections.Generic

Namespace Widgets

    ''' <summary>
    ''' One message (user or assistant) in an AI chat conversation, including any
    ''' artifacts or actions it carried
    ''' </summary>
    Public Class ChatHistoryMessage

        Public Sub New(vRole As String, vContent As String)
            Role = vRole
            Content = vContent
            Timestamp = DateTime.Now()
            Artifacts = New List(Of ArtifactInfo)
            Actions = New List(Of AIAssistantPanel.AIAction)
        End Sub

        Public Property Role As String
        Public Property Content As String
        Public Property Timestamp As DateTime
        Public Property Artifacts As List(Of ArtifactInfo)
        Public Property Actions As List(Of AIAssistantPanel.AIAction)
    End Class

    ''' <summary>
    ''' A single artifact (code block, file, etc.) extracted from an AI chat message
    ''' </summary>
    Public Class ArtifactInfo
        Public Property Id As String
        Public Property Type As String
        Public Property Title As String
        Public Property FilePath As String
        Public Property Content As String
    End Class

End Namespace
