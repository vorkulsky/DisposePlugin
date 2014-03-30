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
                methodArguments.Add(MethodArgumentStatus.Read(reader));
            return new DisposeMethodStatus(name, offset, methodArguments, psiSourceFile);
        }
    }

    public class MethodArgumentStatus
    {
        private byte _number; // Для this равен 0
        private VariableDisposeStatus _status;
        private IList<InvokedMethod> _invokedMethods;

        public MethodArgumentStatus(byte number, VariableDisposeStatus status, IList<InvokedMethod> invokedMethods)
        {
            _number = number;
            _status = status;
            _invokedMethods = invokedMethods;
        }

        #region MethodArgumentStatus Members
        public byte Number
        {
            get { return _number; }
        }

        public VariableDisposeStatus Status
        {
            get { return _status; }
        }

        public IList<InvokedMethod> InvokedMethods
        {
            get { return _invokedMethods; }
        }
        #endregion MethodArgumentStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Number);
            writer.Write(Status.ToString());
            writer.Write(InvokedMethods.Count);
            foreach (var method in InvokedMethods)
                method.Write(writer);
        }

        public static MethodArgumentStatus Read(BinaryReader reader)
        {
            var number = reader.ReadByte();
            var status = (VariableDisposeStatus) Enum.Parse(typeof(VariableDisposeStatus), reader.ReadString());
            var count = reader.ReadInt32();
            var invokedMethods = new List<InvokedMethod>(count);
            for (var i = 0; i < count; i++)
                invokedMethods.Add(InvokedMethod.Read(reader));
            return new MethodArgumentStatus(number, status, invokedMethods);
        }
    }

    public class InvokedMethod
    {
        private string _name;
        private int _offset;
        private byte _argumentPosition; // Для объекта, на котором вызывают, равен 0

        public InvokedMethod(string name, int offset, byte argumentPosition)
        {
            _name = name;
            _offset = offset;
            _argumentPosition = argumentPosition;
        }

        #region InvokedMethod Members
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
        #endregion InvokedMethod Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(ArgumentPosition);
        }

        public static InvokedMethod Read(BinaryReader reader)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var argumentPosition = reader.ReadByte();
            return new InvokedMethod(name, offset, argumentPosition);
        }
    }
}
