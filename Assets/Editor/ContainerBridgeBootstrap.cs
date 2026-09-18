// TYMCZASOWY skrypt bootstrap mostka dla izolowanego kontenera agenta.
// Konfiguruje plugin MCP-for-Unity (CoplayDev) tak, aby hostowal serwer HTTP MCP
// dostepny dla kontenera Docker (bind 0.0.0.0:8080) i startowal automatycznie.
// Po zestawieniu mostka plik moze zostac usuniety (przywraca domyslne loopback).
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class ContainerBridgeBootstrap
{
    static ContainerBridgeBootstrap()
    {
        // Klucze EditorPrefs pluginu MCP-for-Unity (patrz EditorPrefKeys.cs).
        EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
        EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
        EditorPrefs.SetBool("MCPForUnity.Security.AllowLanHttpBind", true);   // pozwala bind 0.0.0.0
        EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://0.0.0.0:8080");  // dostepne dla kontenera
        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);             // start serwera na load

        Debug.Log("[ContainerBridgeBootstrap] Skonfigurowano HTTP MCP na 0.0.0.0:8080 + auto-start.");
    }
}
#endif
