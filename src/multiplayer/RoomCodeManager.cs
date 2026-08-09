using Godot;
using System;
using System.Net;

public static class RoomCodeManager
{
	private const string PREFIX = "MZ-";

	/// <summary>
	/// Convierte una dirección IP (ej. "192.168.1.50") en un código de sala corto (ej. "MZ-C0A80132").
	/// </summary>
	public static string IpToRoomCode(string ipAddress)
	{
		if (string.IsNullOrWhiteSpace(ipAddress)) return "MZ-LOCAL";

		string cleanIp = ipAddress.Trim();
		if (IPAddress.TryParse(cleanIp, out var parsedIp))
		{
			byte[] bytes = parsedIp.GetAddressBytes();
			if (bytes.Length == 4)
			{
				uint ipNum = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | (uint)bytes[3];
				return PREFIX + ipNum.ToString("X8");
			}
		}

		return PREFIX + cleanIp.ToUpper();
	}

	/// <summary>
	/// Convierte un código de sala (ej. "MZ-C0A80132") o dirección IP de vuelta a la IP de conexión IPv4.
	/// </summary>
	public static string RoomCodeToIp(string input)
	{
		if (string.IsNullOrWhiteSpace(input)) return "127.0.0.1";

		string clean = input.Trim().ToUpper();
		if (clean.StartsWith(PREFIX))
		{
			clean = clean.Substring(PREFIX.Length);
		}

		if (clean.Length == 8 && uint.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out uint hexNum))
		{
			byte b1 = (byte)((hexNum >> 24) & 0xFF);
			byte b2 = (byte)((hexNum >> 16) & 0xFF);
			byte b3 = (byte)((hexNum >> 8) & 0xFF);
			byte b4 = (byte)(hexNum & 0xFF);
			return $"{b1}.{b2}.{b3}.{b4}";
		}

		// Si el usuario ingresó directamente una IP (ej. 192.168.1.50), retornarla limpia
		return input.Trim();
	}
}
