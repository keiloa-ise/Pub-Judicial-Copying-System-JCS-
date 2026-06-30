namespace ResourceIQ.Jcs.Application.Abstractions;

/// <summary>Hashes and verifies passwords. Plaintext is never stored or logged .</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
