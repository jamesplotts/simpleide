' Models/TODOItem.vb - Enhanced TODO item model with priority, categories, and metadata
Imports System
Imports System.Collections.Generic

Namespace Models
    Public Class TODOItem
        
        ' Enums
        Public Enum ePriority
            eUnspecified
            eLow
            eMedium
            eHigh
            eCritical
            eLastValue
        End Enum
        
        Public Enum eCategory
            eUnspecified
            eBug
            eFeature
            eDocumentation
            eRefactor
            eTesting
            ePerformance
            eSecurity
            eUI
            eOther
            eLastValue
        End Enum
        
        Public Enum eStatus
            eUnspecified
            ePending
            eInProgress
            eCompleted
            eCancelled
            eLastValue
        End Enum
        
        Public Enum eSourceType
            eUnspecified
            eManual          ' Added manually to TODO.txt
            eCodeComment     ' Found in source code
            eLastValue
        End Enum
        
        ' Properties
        Public Property Id As String = Guid.NewGuid().ToString()
        Public Property Title As String = ""
        Public Property Description As String = ""
        Public Property Priority As ePriority = ePriority.eMedium
        Public Property Category As eCategory = eCategory.eOther
        Public Property Status As eStatus = eStatus.ePending
        Public Property SourceType As eSourceType = eSourceType.eManual
        Public Property CreatedDate As DateTime = DateTime.Now
        Public Property DueDate As DateTime?
        Public Property CompletedDate As DateTime?
        Public Property Progress As Integer = 0  ' 0-100%
        Public Property Tags As New List(Of String)
        
        ' Source information (for code comments)
        Public Property SourceFile As String = ""
        Public Property SourceLine As Integer = 0
        Public Property SourceColumn As Integer = 0
        Public Property SourceText As String = ""
        
        ' Git information
        Public Property GitBranch As String = ""
        Public Property GitCommit As String = ""
        
        ' Metadata
        Public Property AssignedTo As String = ""
        Public Property EstimatedHours As Double = 0
        Public Property ActualHours As Double = 0
        
        Public Sub New()
            ' Default constructor
        End Sub
        
        Public Sub New(vTitle As String)
            Title = vTitle
        End Sub
        
        Public Sub New(vTitle As String, vDescription As String)
            Title = vTitle
            Description = vDescription
        End Sub
        
        ' Factory method for code comments
        Public Shared Function FromCodeComment(vTitle As String, vFilePath As String, vLine As Integer, vColumn As Integer, vSourceText As String) As TODOItem
            Dim lItem As New TODOItem(vTitle)
            lItem.SourceType = eSourceType.eCodeComment
            lItem.SourceFile = vFilePath
            lItem.SourceLine = vLine
            lItem.SourceColumn = vColumn
            lItem.SourceText = vSourceText
            lItem.Category = eCategory.eOther  ' Default for code comments
            Return lItem
        End Function
        
        ' Helper methods
        Public Function GetPriorityDisplayText() As String
            Select Case Priority
                Case ePriority.eLow
                    Return "Low"
                Case ePriority.eMedium
                    Return "Medium"
                Case ePriority.eHigh
                    Return "High"
                Case ePriority.eCritical
                    Return "Critical"
                Case Else
                    Return "Unspecified"
            End Select
        End Function
        
        Public Function GetCategoryDisplayText() As String
            Select Case Category
                Case eCategory.eBug
                    Return "Bug"
                Case eCategory.eFeature
                    Return "Feature"
                Case eCategory.eDocumentation
                    Return "documentation"
                Case eCategory.eRefactor
                    Return "Refactor"
                Case eCategory.eTesting
                    Return "Testing"
                Case eCategory.ePerformance
                    Return "Performance"
                Case eCategory.eSecurity
                    Return "Security"
                Case eCategory.eUI
                    Return "UI/UX"
                Case eCategory.eOther
                    Return "Other"
                Case Else
                    Return "Unspecified"
            End Select
        End Function
        
        Public Function GetStatusDisplayText() As String
            Select Case Status
                Case eStatus.ePending
                    Return "Pending"
                Case eStatus.eInProgress
                    Return "in Progress"
                Case eStatus.eCompleted
                    Return "Completed"
                Case eStatus.eCancelled
                    Return "Cancelled"
                Case Else
                    Return "Unspecified"
            End Select
        End Function
        
        Public Function GetPriorityColor() As String
            Select Case Priority
                Case ePriority.eLow
                    Return "#28a745"      ' Green
                Case ePriority.eMedium
                    Return "#ffc107"      ' Orange
                Case ePriority.eHigh
                    Return "#fd7e14"      ' Dark Orange
                Case ePriority.eCritical
                    Return "#dc3545"      ' Red
                Case Else
                    Return "#6c757d"      ' Gray
            End Select
        End Function
        
        Public Function GetCategoryIcon() As String
            Select Case Category
                Case eCategory.eBug
                    Return "bug-symbolic"
                Case eCategory.eFeature
                    Return "starred-symbolic"
                Case eCategory.eDocumentation
                    Return "help-browser-symbolic"
                Case eCategory.eRefactor
                    Return "preferences-system-symbolic"
                Case eCategory.eTesting
                    Return "checkbox-symbolic"
                Case eCategory.ePerformance
                    Return "applications-utilities-symbolic"
                Case eCategory.eSecurity
                    Return "security-high-symbolic"
                Case eCategory.eUI
                    Return "applications-graphics-symbolic"
                Case Else
                    Return "Text-x-generic-symbolic"
            End Select
        End Function
        
        Public Function IsOverdue() As Boolean
            Return DueDate.HasValue AndAlso DueDate.Value < DateTime.Now AndAlso Status <> eStatus.eCompleted
        End Function
        
        Public Function IsDueSoon() As Boolean
            Return DueDate.HasValue AndAlso DueDate.Value <= DateTime.Now.AddDays(3) AndAlso Status <> eStatus.eCompleted
        End Function
        
        Public Function GetDisplayTitle() As String
            Dim lTitle As String = If(String.IsNullOrEmpty(Title), "Untitled TODO", Title)
            
            ' Add progress indicator if in progress
            If Status = eStatus.eInProgress AndAlso Progress > 0 Then
                lTitle = $"[{Progress}%] {lTitle}"
            End If
            
            ' Add overdue indicator
            If IsOverdue() Then
                lTitle = $"⚠️ {lTitle}"
            ElseIf IsDueSoon() Then
                lTitle = $"⏰ {lTitle}"
            End If
            
            Return lTitle
        End Function
        
        Public Function GetFormattedDescription() As String
            Dim lParts As New List(Of String)
            
            If Not String.IsNullOrEmpty(Description) Then
                lParts.Add(Description)
            End If
            
            ' Add source information for code comments
            If SourceType = eSourceType.eCodeComment AndAlso Not String.IsNullOrEmpty(SourceFile) Then
                lParts.Add($"Source: {System.IO.Path.GetFileName(SourceFile)}:{SourceLine}")
            End If
            
            ' Add due date information
            If DueDate.HasValue Then
                Dim lDueDateStr As String = DueDate.Value.ToString("yyyy-MM-dd")
                If IsOverdue() Then
                    lParts.Add($"Overdue: {lDueDateStr}")
                ElseIf IsDueSoon() Then
                    lParts.Add($"Due soon: {lDueDateStr}")
                Else
                    lParts.Add($"Due: {lDueDateStr}")
                End If
            End If
            
            ' Add tags
            If Tags.Count > 0 Then
                lParts.Add($"Tags: {String.Join(", ", Tags)}")
            End If
            
            Return String.Join(Environment.NewLine, lParts)
        End Function
        
        Public Overrides Function ToString() As String
            Return GetDisplayTitle()
        End Function
        
    End Class
End Namespace

