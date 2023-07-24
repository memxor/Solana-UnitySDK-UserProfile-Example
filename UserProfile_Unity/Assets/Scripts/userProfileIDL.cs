using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using UserProfileBackend;
using UserProfileBackend.Program;
using UserProfileBackend.Errors;
using UserProfileBackend.Accounts;

namespace UserProfileBackend
{
    namespace Accounts
    {
        public partial class UserProfile
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 13983031102394541344UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{32, 37, 119, 205, 179, 180, 13, 194};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "6NrstEfxKVB";
            public string Name { get; set; }

            public string Username { get; set; }

            public string Email { get; set; }

            public static UserProfile Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                UserProfile result = new UserProfile();
                offset += _data.GetBorshString(offset, out var resultName);
                result.Name = resultName;
                offset += _data.GetBorshString(offset, out var resultUsername);
                result.Username = resultUsername;
                offset += _data.GetBorshString(offset, out var resultEmail);
                result.Email = resultEmail;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum UserProfileBackendErrorKind : uint
        {
        }
    }

    public partial class UserProfileBackendClient : TransactionalBaseClient<UserProfileBackendErrorKind>
    {
        public UserProfileBackendClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserProfile>>> GetUserProfilesAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = UserProfile.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserProfile>>(res);
            List<UserProfile> resultingAccounts = new List<UserProfile>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => UserProfile.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<UserProfile>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<UserProfile>> GetUserProfileAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful || res.Result?.Value == null)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<UserProfile>(res);
            var resultingAccount = UserProfile.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<UserProfile>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeUserProfileAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, UserProfile> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                UserProfile parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = UserProfile.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitUserprofileAsync(InitUserprofileAccounts accounts, string name, string username, string email, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.UserProfileBackendProgram.InitUserprofile(accounts, name, username, email, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<UserProfileBackendErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<UserProfileBackendErrorKind>>{};
        }
    }

    namespace Program
    {
        public class InitUserprofileAccounts
        {
            public PublicKey UserProfile { get; set; }

            public PublicKey User { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public static class UserProfileBackendProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitUserprofile(InitUserprofileAccounts accounts, string name, string username, string email, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.UserProfile, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(7331778417231312005UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(name, offset);
                offset += _data.WriteBorshString(username, offset);
                offset += _data.WriteBorshString(email, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}