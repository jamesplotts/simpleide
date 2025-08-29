' Models/DataGridModels.vb - Data models for CustomDrawDataGrid
Imports System
Imports System.Collections.Generic

Namespace Models
    
    ' ===== Column Models =====
    
    ''' <summary>
    ''' Represents a column definition in the data grid
    ''' </summary>
    Public Class DataGridColumn
        Public Property Name As String
        Public Property Title As String
        Public Property Width As Integer = 100
        Public Property MinWidth As Integer = 30
        Public Property MaxWidth As Integer = Integer.MaxValue
        Public Property Visible As Boolean = True
        Public Property Resizable As Boolean = True
        Public Property Sortable As Boolean = True
        Public Property DataType As DataGridColumnType = DataGridColumnType.eText
        Public Property Alignment As ColumnAlignment = ColumnAlignment.eLeft
        Public Property Ellipsize As Boolean = False
        Public Property ColumnType As DataGridColumnType = DataGridColumnType.eUnspecified
        Public Property Tag As Object
        Public Property AutoExpand As Boolean = False      ' Column should expand to fill remaining space
        Public Property WordWrap As Boolean = False        ' Enable word wrapping for this column
        Public Property MaxHeight As Integer = 100         ' Maximum height when word wrapping

    End Class
    
    ''' <summary>
    ''' Specifies the data type of a column
    ''' </summary>
    Public Enum DataGridColumnType
        eUnspecified
        eText
        eNumber
        eBoolean
        eDate
        eIcon
        eLastValue
    End Enum
    
    ''' <summary>
    ''' Specifies the alignment of column content
    ''' </summary>
    Public Enum ColumnAlignment
        eUnspecified
        eLeft
        eCenter
        eRight
        eLastValue
    End Enum
    
    ' ===== Row Models =====
    
    ''' <summary>
    ''' Represents a row of data in the grid
    ''' </summary>
    Public Class DataGridRow
        Public Property Cells As New List(Of DataGridCell)
        Public Property Tag As Object
        Public Property Selected As Boolean = False
        Public Property Visible As Boolean = True
        Public Property Style As RowStyle = RowStyle.eNormal
        Public Property Height As Integer = 0  ' 0 means use default
        Public Property Index As Integer
        Public Property CalculatedHeight As Integer = 0    ' Calculated height based on content

    End Class
    
    ''' <summary>
    ''' Specifies the visual style of a row
    ''' </summary>
    Public Enum RowStyle
        eUnspecified
        eNormal
        eError
        eWarning
        eSuccess
        eInfo
        eDisabled
        eLastValue
    End Enum
    
    ' ===== Cell Models =====
    
    ''' <summary>
    ''' Represents a single cell in the data grid
    ''' </summary>
    Public Class DataGridCell
        Public Property Value As Object
        Public Property DisplayText As String
        Public Property ToolTip As String
        Public Property Editable As Boolean = False
        Public Property Tag As Object
        Public Property ForegroundColor As String
        Public Property BackgroundColor As String
        Public Property Bold As Boolean = False
        Public Property Italic As Boolean = False
        
        ''' <summary>
        ''' Creates a new DataGridCell with a value
        ''' </summary>
        Public Sub New()
        End Sub
        
        ''' <summary>
        ''' Creates a new DataGridCell with a value
        ''' </summary>
        Public Sub New(vValue As Object)
            Value = vValue
            If vValue IsNot Nothing Then
                DisplayText = vValue.ToString()
            End If
        End Sub
        
        ''' <summary>
        ''' Creates a new DataGridCell with a value and display text
        ''' </summary>
        Public Sub New(vValue As Object, vDisplayText As String)
            Value = vValue
            DisplayText = vDisplayText
        End Sub
    End Class
    
    ' ===== Filter Models =====
    
    ''' <summary>
    ''' Represents a filter applied to the data grid
    ''' </summary>
    Public Class DataGridFilter
        Public Property ColumnIndex As Integer
        Public Property FilterType As FilterType
        Public Property FilterValue As Object
        Public Property CaseSensitive As Boolean = False
    End Class
    
    ''' <summary>
    ''' Specifies the type of filter to apply
    ''' </summary>
    Public Enum FilterType
        eUnspecified
        eContains
        eEquals
        eStartsWith
        eEndsWith
        eGreaterThan
        eLessThan
        eBetween
        eCustom
        eLastValue
    End Enum

    ''' <summary>
    ''' Event arguments for custom icon rendering
    ''' </summary>
    Public Class IconRenderEventArgs
        Inherits EventArgs
        
        ''' <summary>
        ''' The Cairo context to draw with
        ''' </summary>
        Public ReadOnly Property Context As Cairo.Context
        
        ''' <summary>
        ''' The cell containing the icon data
        ''' </summary>
        Public ReadOnly Property Cell As DataGridCell
        
        ''' <summary>
        ''' X position to draw at
        ''' </summary>
        Public ReadOnly Property X As Double
        
        ''' <summary>
        ''' Y position to draw at
        ''' </summary>
        Public ReadOnly Property Y As Double
        
        ''' <summary>
        ''' Width available for drawing
        ''' </summary>
        Public ReadOnly Property Width As Double
        
        ''' <summary>
        ''' Height available for drawing
        ''' </summary>
        Public ReadOnly Property Height As Double
        
        ''' <summary>
        ''' Set to True if the icon was rendered
        ''' </summary>
        Public Property Handled As Boolean = False
        
        ''' <summary>
        ''' Creates new icon render event arguments
        ''' </summary>
        Public Sub New(vContext As Cairo.Context, vCell As DataGridCell, 
                       vX As Double, vY As Double, vWidth As Double, vHeight As Double)
            _Context = vContext
            _Cell = vCell
            _X = vX
            _Y = vY
            _Width = vWidth
            _Height = vHeight
        End Sub
    End Class
    
End Namespace