' CodeSenseTypeInfo.vb

Imports System

Namespace Syntax
    
    ''' <summary>
    ''' Type information class for CodeSense
    ''' </summary>
    Friend Class CodeSenseTypeInfo
        Public Property Name As String
        Public Property FullName As String
        Public Property [Namespace] As String
        Public Property IsClass As Boolean
        Public Property IsInterface As Boolean
        Public Property IsValueType As Boolean
        Public Property IsEnum As Boolean
        Public Property Type As Type
        
        ''' <summary>
        ''' Creates a new CodeSenseTypeInfo instance
        ''' </summary>
        Public Sub New(vName As String, vFullName As String, vNamespace As String, 
                       vIsClass As Boolean, vIsInterface As Boolean, 
                       vIsValueType As Boolean, vIsEnum As Boolean)
            Name = vName
            FullName = vFullName
            [Namespace] = vNamespace
            IsClass = vIsClass
            IsInterface = vIsInterface
            IsValueType = vIsValueType
            IsEnum = vIsEnum
        End Sub

    End Class

End Namespace

