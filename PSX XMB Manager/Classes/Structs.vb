Public Class Structs

    Public Structure MountedPSXDrive
        Private _HDLDriveName As String
        Private _NBDDriveName As String
        Private _DriveID As String
        Private _ConnectedOnIP As String

        Public Property DriveID As String
            Get
                Return _DriveID
            End Get
            Set
                _DriveID = Value
            End Set
        End Property

        Public Property HDLDriveName As String
            Get
                Return _HDLDriveName
            End Get
            Set
                _HDLDriveName = Value
            End Set
        End Property

        Public Property NBDDriveName As String
            Get
                Return _NBDDriveName
            End Get
            Set
                _NBDDriveName = Value
            End Set
        End Property

        Public Property ConnectedOnIP As String
            Get
                Return _ConnectedOnIP
            End Get
            Set
                _ConnectedOnIP = Value
            End Set
        End Property
    End Structure

    Public Structure HDL_Dump_Args
        Private _Args As String()
        Private _Command As String

        Public Property Command As String
            Get
                Return _Command
            End Get
            Set
                _Command = Value
            End Set
        End Property

        Public Property Args As String()
            Get
                Return _Args
            End Get
            Set
                _Args = Value
            End Set
        End Property
    End Structure

    Public Structure Partition
        Private _Type As String
        Private _Start As String
        Private _Parts As String
        Private _Size As String
        Private _Name As String

        Public Property Type As String
            Get
                Return _Type
            End Get
            Set
                _Type = Value
            End Set
        End Property

        Public Property Start As String
            Get
                Return _Start
            End Get
            Set
                _Start = Value
            End Set
        End Property

        Public Property Parts As String
            Get
                Return _Parts
            End Get
            Set
                _Parts = Value
            End Set
        End Property

        Public Property Size As String
            Get
                Return _Size
            End Get
            Set
                _Size = Value
            End Set
        End Property

        Public Property Name As String
            Get
                Return _Name
            End Get
            Set
                _Name = Value
            End Set
        End Property
    End Structure

    Public Structure GamePartition
        Private _Type As String
        Private _Size As String
        Private _Name As String
        Private _Flags As String
        Private _DMA As String
        Private _Startup As String

        Public Property Type As String
            Get
                Return _Type
            End Get
            Set
                _Type = Value
            End Set
        End Property

        Public Property Size As String
            Get
                Return _Size
            End Get
            Set
                _Size = Value
            End Set
        End Property

        Public Property Flags As String
            Get
                Return _Flags
            End Get
            Set
                _Flags = Value
            End Set
        End Property

        Public Property DMA As String
            Get
                Return _DMA
            End Get
            Set
                _DMA = Value
            End Set
        End Property

        Public Property Startup As String
            Get
                Return _Startup
            End Get
            Set
                _Startup = Value
            End Set
        End Property

        Public Property Name As String
            Get
                Return _Name
            End Get
            Set
                _Name = Value
            End Set
        End Property

    End Structure

    Public Enum AssetType
        Audio
        DIC
        Font
        Image
        Video
        XML
    End Enum

    Public Structure AssetListViewItem
        Private _AssetFileName As String
        Private _AssetFilePath As String
        Private _Type As AssetType
        Private _Icon As ImageSource

        Public Property AssetFileName As String
            Get
                Return _AssetFileName
            End Get
            Set
                _AssetFileName = Value
            End Set
        End Property

        Public Property AssetFilePath As String
            Get
                Return _AssetFilePath
            End Get
            Set
                _AssetFilePath = Value
            End Set
        End Property

        Public Property Type As AssetType
            Get
                Return _Type
            End Get
            Set
                _Type = Value
            End Set
        End Property

        Public Property Icon As ImageSource
            Get
                Return _Icon
            End Get
            Set
                _Icon = Value
            End Set
        End Property

    End Structure

End Class
