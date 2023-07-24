using System.Text;
using Solana.Unity.Wallet;
using UnityEngine;
using Solana.Unity.SDK;
using Solana.Unity.Rpc.Models;
using UserProfileBackend.Program;
using UserProfileBackend;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using TMPro;
using UnityEngine.UI;
using Solana.Unity.Programs;

public class SolanaManager : MonoBehaviour
{
    public static PublicKey programId = new("71XE3Cj3mMdunqP7LaTQjuUNe7EA7qHbuD6YJFJao2Rq");
    private PublicKey userProfilePDA;

    [Header("New User Profile")]
    public GameObject createUserProfilePanel;
    public TMP_InputField newName;
    public TMP_InputField newUsername;
    public TMP_InputField newEmail;
    public Button submitButton;

    [Header("Display User Profile")]
    public GameObject displayUserProfilePanel;
    public TextMeshProUGUI displayName;
    public TextMeshProUGUI displayUsername;
    public TextMeshProUGUI displayEmail;

    private void Awake()
    {
        submitButton.onClick.AddListener(() => OnSubmitButton());
        Web3.OnLogin += _ => {
            PublicKey.TryFindProgramAddress(new[]{
            Encoding.UTF8.GetBytes("User"),
            Web3.Account.PublicKey
            }, programId, out userProfilePDA, out var bump);
            TryGetUserProfile();
        };
    }

    private async void TryGetUserProfile()
    {
        var backendClient = new UserProfileBackendClient(Web3.Rpc, Web3.WsRpc, programId);
        var res = await backendClient.GetUserProfileAsync(userProfilePDA);
        if (res.ParsedResult == null)
        {
            createUserProfilePanel.SetActive(true);
            displayUserProfilePanel.SetActive(false);
        }
        else
        {
            displayName.text = $"Name: {res.ParsedResult.Name}";
            displayUsername.text = $"Username: {res.ParsedResult.Username}";
            displayEmail.text = $"Email: {res.ParsedResult.Email}";
            createUserProfilePanel.SetActive(false);
            displayUserProfilePanel.SetActive(true);
        }
    }

    private async void OnSubmitButton()
    {
        InitUserprofileAccounts accounts = new()
        {
            UserProfile = userProfilePDA,
            User = Web3.Account.PublicKey,
            SystemProgram = SystemProgram.ProgramIdKey
        };

        var rpcClient = ClientFactory.GetClient(Cluster.DevNet);
        TransactionInstruction ix = UserProfileBackendProgram.InitUserprofile(accounts, newName.text, newUsername.text, newEmail.text, programId);
        var recentHash = (await rpcClient.GetRecentBlockHashAsync()).Result.Value.Blockhash;

        var tx = new TransactionBuilder().
            SetFeePayer(Web3.Account).
            AddInstruction(ix).
            SetRecentBlockHash(recentHash).
            Build(Web3.Account);
        await rpcClient.SendTransactionAsync(tx);
        Invoke(nameof(TryGetUserProfile), 5);
    }
}