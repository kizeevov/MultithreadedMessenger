﻿using System;
using System.Windows.Input;

namespace MessageCommonLib
{
    public class DelegateCommand : ICommand
    {
        #region Constructor

        public DelegateCommand(Action<object> action)
        {
            ExecuteDelegate = action;
        }

        #endregion


        #region Properties

        public Predicate<object> CanExecuteDelegate { get; set; }
        public Action<object> ExecuteDelegate { get; set; }

        #endregion


        #region ICommand Members

        public bool CanExecute(object parameter)
        {
            if (CanExecuteDelegate != null)
            {
                return CanExecuteDelegate(parameter);
            }

            return true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            if (ExecuteDelegate != null)
            {
                ExecuteDelegate(parameter);
            }
        }

        #endregion
    }
}