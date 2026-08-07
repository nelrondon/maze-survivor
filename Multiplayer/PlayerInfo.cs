// Class used to contain the relevant information of each player. Required for passing information from one location to another via the sendPlayerInformation method.
public class PlayerInfo
{
	public string Name { get; set; }
	public int Id { get; set; }
	public bool IsSpectator { get; set; } = false;

	// NOTE (DiGiorgio-L): Might add a tracker for each player's score
	// public int Score { get; set; }

	public override bool Equals(object obj)
	{
		if (obj is PlayerInfo other)
		{
			return Id == other.Id;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Id.GetHashCode();
	}
}