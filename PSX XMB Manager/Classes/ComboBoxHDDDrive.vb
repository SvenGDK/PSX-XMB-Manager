Imports System.ComponentModel

Public Class ComboBoxHDDDrive

    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyPropertyChanged(propName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propName))
    End Sub

    Private _DevicePath As String
    Private _DeviceSize As ULong
    Private _DeviceInterfaceType As String
    Private _DeviceMediaType As String
    Private _DeviceCaption As String
    Private _ComboBoxDisplayText As String

    Public Property DevicePath As String
        Get
            Return _DevicePath
        End Get
        Set
            _DevicePath = Value
            NotifyPropertyChanged("DevicePath")
        End Set
    End Property

    Public Property DeviceSize As ULong
        Get
            Return _DeviceSize
        End Get
        Set
            _DeviceSize = Value
            NotifyPropertyChanged("DeviceSize")
        End Set
    End Property

    Public Property DeviceInterfaceType As String
        Get
            Return _DeviceInterfaceType
        End Get
        Set
            _DeviceInterfaceType = Value
            NotifyPropertyChanged("DeviceInterfaceType")
        End Set
    End Property

    Public Property DeviceMediaType As String
        Get
            Return _DeviceMediaType
        End Get
        Set
            _DeviceMediaType = Value
            NotifyPropertyChanged("DeviceMediaType")
        End Set
    End Property

    Public Property DeviceCaption As String
        Get
            Return _DeviceCaption
        End Get
        Set
            _DeviceCaption = Value
            NotifyPropertyChanged("DeviceCaption")
        End Set
    End Property

    Public Property ComboBoxDisplayText As String
        Get
            Return _ComboBoxDisplayText
        End Get
        Set
            _ComboBoxDisplayText = Value
            NotifyPropertyChanged("ComboBoxDisplayText")
        End Set
    End Property

End Class
