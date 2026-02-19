Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows.Input

Namespace Commands
    Public Class AsyncRelayCommand
        Implements ICommand

        Private ReadOnly _execute As Func(Of Object, Task)
        Private ReadOnly _canExecute As Predicate(Of Object)
        Private ReadOnly _onException As Action(Of Exception)
        Private _isExecuting As Boolean

        Public Sub New(execute As Func(Of Task), Optional canExecute As Predicate(Of Object) = Nothing, Optional onException As Action(Of Exception) = Nothing)
            If execute Is Nothing Then
                Throw New ArgumentNullException(NameOf(execute))
            End If

            _execute = Function(parameter) execute()
            _canExecute = canExecute
            _onException = If(onException, AddressOf LogException)
        End Sub

        Public Sub New(execute As Func(Of Object, Task), Optional canExecute As Predicate(Of Object) = Nothing, Optional onException As Action(Of Exception) = Nothing)
            If execute Is Nothing Then
                Throw New ArgumentNullException(NameOf(execute))
            End If

            _execute = execute
            _canExecute = canExecute
            _onException = If(onException, AddressOf LogException)
        End Sub

        Public Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged

        Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
            If _isExecuting Then
                Return False
            End If

            If _canExecute Is Nothing Then
                Return True
            End If

            Return _canExecute(parameter)
        End Function

        Public Sub Execute(parameter As Object) Implements ICommand.Execute
            Dim pending = ExecuteAsync(parameter)
            pending.ContinueWith(
                Sub(t)
                    ObserveFault(t.Exception)
                End Sub,
                TaskContinuationOptions.OnlyOnFaulted)
        End Sub

        Public Async Function ExecuteAsync(parameter As Object) As Task
            If Not CanExecute(parameter) Then
                Return
            End If

            _isExecuting = True
            RaiseCanExecuteChanged()

            Try
                Await _execute(parameter).ConfigureAwait(True)
            Finally
                _isExecuting = False
                RaiseCanExecuteChanged()
            End Try
        End Function

        Public Sub RaiseCanExecuteChanged()
            RaiseEvent CanExecuteChanged(Me, EventArgs.Empty)
        End Sub

        Private Sub ObserveFault(fault As AggregateException)
            If fault Is Nothing Then
                Return
            End If

            For Each inner As Exception In fault.Flatten().InnerExceptions
                Try
                    _onException(inner)
                Catch
                    ' Never throw from the exception observer.
                End Try
            Next
        End Sub

        Private Shared Sub LogException(ex As Exception)
            If ex Is Nothing Then
                Return
            End If

            Trace.TraceError($"AsyncRelayCommand exception: {ex}")
        End Sub
    End Class
End Namespace
