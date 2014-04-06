using System;
using System.Collections.Generic;
using System.IO;
using DisposePlugin.Services;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Cache
{
    public class DisposeMethodStatus
    {
        private readonly IPsiSourceFile _psiSourceFile;
        private string _name;
        private int _offset;
        private IList<MethodArgumentStatus> _methodArguments;

        public DisposeMethodStatus(string name, int offset, IList<MethodArgumentStatus> methodArguments, IPsiSourceFile psiSourceFile)
        {
            _name = name;
            _offset = offset;
            _methodArguments = methodArguments;
            _psiSourceFile = psiSourceFile;
        }

        #region DisposeMethodStatus Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }

        public string Name
        {
            get { return _name; }
        }

        public int Offset
        {
            get { return _offset; }
        }

        public IList<MethodArgumentStatus> MethodArguments
        {
            get { return _methodArguments; }
        }
        #endregion DisposeMethodStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(MethodArguments.Count);
            foreach (var argument in MethodArguments)
                argument.Write(writer);
        }

        public static DisposeMethodStatus Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var count = reader.ReadInt32();
            var methodArguments = new List<MethodArgumentStatus>(count);
            for (var i = 0; i < count; i++)
                methodArguments.Add(MethodArgumentStatus.Read(reader, psiSourceFile));
            return new DisposeMethodStatus(name, offset, methodArguments, psiSourceFile);
        }
    }

    public class MethodArgumentStatus
    {
        private readonly IPsiSourceFile _psiSourceFile;
        private byte _number; // Для this равен 0
        private VariableDisposeStatus _status;
        private IList<InvokedExpression> _invokedExpressions;

        public MethodArgumentStatus(byte number, VariableDisposeStatus status, IList<InvokedExpression> invokedExpressions, IPsiSourceFile psiSourceFile)
        {
            _number = number;
            _status = status;
            _invokedExpressions = invokedExpressions;
            _psiSourceFile = psiSourceFile;
        }

        #region MethodArgumentStatus Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }

        public byte Number
        {
            get { return _number; }
        }

        public VariableDisposeStatus Status
        {
            get { return _status; }
        }

        public IList<InvokedExpression> InvokedExpressions
        {
            get { return _invokedExpressions; }
        }
        #endregion MethodArgumentStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Number);
            writer.Write(Status.ToString());
            writer.Write(InvokedExpressions.Count);
            foreach (var expression in InvokedExpressions)
                expression.Write(writer);
        }

        public static MethodArgumentStatus Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var number = reader.ReadByte();
            var status = (VariableDisposeStatus) Enum.Parse(typeof(VariableDisposeStatus), reader.ReadString());
            var count = reader.ReadInt32();
            var invokedExpressions = new List<InvokedExpression>(count);
            for (var i = 0; i < count; i++)
                invokedExpressions.Add(InvokedExpression.Read(reader, psiSourceFile));
            return new MethodArgumentStatus(number, status, invokedExpressions, psiSourceFile);
        }
    }

    public class InvokedExpression
    {
        private readonly IPsiSourceFile _psiSourceFile;
        // Имя вызываемого метода
        private string _name;
        // Offset вызова метода
        private int _offset;
        // Номер аргумента в выражении вызова метода
        // Для объекта, на котором вызывают, равен 0
        private byte _argumentPosition;

        public InvokedExpression(string name, int offset, byte argumentPosition, IPsiSourceFile psiSourceFile)
        {
            _name = name;
            _offset = offset;
            _argumentPosition = argumentPosition;
            _psiSourceFile = psiSourceFile;
        }

        #region InvokedExpression Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }
        public string Name
        {
            get { return _name; }
        }

        public int Offset
        {
            get { return _offset; }
        }

        public byte ArgumentPosition
        {
            get { return _argumentPosition; }
        }
        #endregion InvokedExpression Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(ArgumentPosition);
        }

        public static InvokedExpression Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var argumentPosition = reader.ReadByte();
            return new InvokedExpression(name, offset, argumentPosition, psiSourceFile);
        }
    }
}
