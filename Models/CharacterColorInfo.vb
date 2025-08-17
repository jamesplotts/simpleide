' Models/CharacterColorInfo.vb - Character color information for syntax highlighting
Imports System

Namespace Models
    
    ''' <summary>
    ''' Represents color information for a single character in the editor
    ''' </summary>
    Public Class CharacterColorInfo
        
        Private pColor As String
        Private pIsBold As Boolean
        Private pIsItalic As Boolean
        
        ''' <summary>
        ''' Gets or sets the color as a hex string (e.g., "#FF0000")
        ''' </summary>
        Public Property Color As String
            Get
                Return pColor
            End Get
            Set(Value As String)
                pColor = Value
                pCairoColor = HexToCairoColor(pColor)
            End Set
        End Property

        Private pCairoColor As Cairo.Color 

        Public ReadOnly Property CairoColor() As Cairo.Color
            Get
                Return pCairoColor
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets whether the character should be bold
        ''' </summary>
        Public Property IsBold As Boolean
            Get
                Return pIsBold
            End Get
            Set(Value As Boolean)
                pIsBold = Value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether the character should be italic
        ''' </summary>
        Public Property IsItalic As Boolean
            Get
                Return pIsItalic
            End Get
            Set(Value As Boolean)
                pIsItalic = Value
            End Set
        End Property
        
        ''' <summary>
        ''' Creates a new CharacterColorInfo with default color
        ''' </summary>
        Public Sub New()
            Color = "#D4D4D4" ' Default foreground Color
            pIsBold = False
            pIsItalic = False
        End Sub
        
        ''' <summary>
        ''' Creates a new CharacterColorInfo with specified color
        ''' </summary>
        Public Sub New(vColor As String)
            Color = vColor
            pIsBold = False
            pIsItalic = False
        End Sub
        
        ''' <summary>
        ''' Creates a new CharacterColorInfo with all properties
        ''' </summary>
        Public Sub New(vColor As String, vBold As Boolean, vItalic As Boolean)
            Color = vColor
            pIsBold = vBold
            pIsItalic = vItalic
        End Sub
        
        ''' <summary>
        ''' Creates a copy of this CharacterColorInfo
        ''' </summary>
        Public Function Clone() As CharacterColorInfo
            Return New CharacterColorInfo(pColor, pIsBold, pIsItalic)
        End Function
        
        ''' <summary>
        ''' Checks if this color info equals another
        ''' </summary>
        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot CharacterColorInfo Then Return False
            
            Dim lOther As CharacterColorInfo = DirectCast(obj, CharacterColorInfo)
            Return pColor = lOther.Color AndAlso 
                   pIsBold = lOther.IsBold AndAlso 
                   pIsItalic = lOther.IsItalic
        End Function
        
        ''' <summary>
        ''' Gets hash code for this color info
        ''' </summary>
        Public Overrides Function GetHashCode() As Integer
            Return pColor.GetHashCode() Xor pIsBold.GetHashCode() Xor pIsItalic.GetHashCode()
        End Function

        Private Function HexToCairoColor(hex As String) As Cairo.Color
            ' Remove the '#' prefix
            hex = hex.TrimStart("#"c)
            
            ' Parse hex components
            Dim r As Byte = Convert.ToByte(hex.Substring(0, 2), 16)
            Dim g As Byte = Convert.ToByte(hex.Substring(2, 2), 16)
            Dim b As Byte = Convert.ToByte(hex.Substring(4, 2), 16)
            
            ' Convert to Cairo's [0.0, 1.0] range
            Return New Cairo.Color(r / 255.0, g / 255.0, b / 255.0)
        End Function
        
    End Class
    
End Namespace
