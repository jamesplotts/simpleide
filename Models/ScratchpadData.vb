' ScratchpadData.vb
' Created: 2025-08-05 20:40:32
' ScratchpadData.vb - Model for scratchpad data
Imports System
Imports System.Collections.Generic
Imports System.Xml.Serialization

Namespace Models
    
    Public Class ScratchpadData
        
        ' Properties
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property Name As String = "Untitled Scratchpad"
        Public Property Content As String = ""
        Public Property Scope As ScratchpadScope = ScratchpadScope.eGlobal
        Public Property Created As DateTime = DateTime.Now
        Public Property LastModified As DateTime = DateTime.Now
        Public Property Tags As New List(Of String)()
        Public Property IsActive As Boolean = True
        
        ' Constructor
        Public Sub New()
        End Sub
        
        Public Sub New(vName As String, vScope As ScratchpadScope)
            Name = vName
            Scope = vScope
        End Sub
        
    End Class
    
    ' Enum for scratchpad scope
    Public Enum ScratchpadScope
        eUnspecified
        eGlobal      ' Stored in user settings, available across all projects
        eProject     ' Stored with project, only available in that project
        eLastValue
    End Enum
    
    ' Collection class for multiple scratchpads
    Public Class ScratchpadCollection
        
        Public Property Scratchpads As New List(Of ScratchpadData)()
        Public Property ActiveScratchpadId As String = ""
        
        ' Get active scratchpad
        Public Function GetActiveScratchpad() As ScratchpadData
            If String.IsNullOrEmpty(ActiveScratchpadId) Then
                Return Nothing
            End If
            
            Return Scratchpads.FirstOrDefault(Function(s) s.Id = ActiveScratchpadId)
        End Function
        
        ' Set active scratchpad
        Public Sub SetActiveScratchpad(vId As String)
            If Scratchpads.any(Function(s) s.Id = vId) Then
                ActiveScratchpadId = vId
            End If
        End Sub
        
        ' Add new scratchpad
        Public Function AddScratchpad(vName As String, vScope As ScratchpadScope) As ScratchpadData
            Dim lScratchpad As New ScratchpadData(vName, vScope)
            Scratchpads.Add(lScratchpad)
            
            ' Make it active if it's the first one
            If Scratchpads.Count = 1 Then
                ActiveScratchpadId = lScratchpad.Id
            End If
            
            Return lScratchpad
        End Function
        
        ' Remove scratchpad
        Public Sub RemoveScratchpad(vId As String)
            Scratchpads.RemoveAll(Function(s) s.Id = vId)
            
            ' If removed was active, activate first remaining
            If ActiveScratchpadId = vId AndAlso Scratchpads.Count > 0 Then
                ActiveScratchpadId = Scratchpads(0).Id
            ElseIf Scratchpads.Count = 0 Then
                ActiveScratchpadId = ""
            End If
        End Sub
        
    End Class
    
End Namespace