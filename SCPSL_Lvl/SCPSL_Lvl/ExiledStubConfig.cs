using Exiled.API.Interfaces;

namespace SCPSL_Lvl
{
    /// <summary>
    /// Минимальный конфиг для EXILED, чтобы плагин мог наследоваться от Plugin<T>.
    /// Здесь можно хранить лишь самое необходимое, например "IsEnabled" и "Debug".
    /// </summary>
    public class ExiledStubConfig : IConfig
    {
        /// <summary>
        /// Требуется интерфейсом IConfig. Позволяет включать/выключать плагин через EXILED.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Можно использовать EXILED Debug-режим (если захотите).
        /// </summary>
        public bool Debug { get; set; } = false;
    }
}
