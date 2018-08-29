using System.Linq;
using System.Threading.Tasks;
using DcrdClient;
using Decred.BlockExplorer;
using Lykke.Service.BlockchainApi.Contract;
using Lykke.Service.BlockchainApi.Contract.Balances;
using Lykke.Service.Decred.Api.Common;
using Lykke.Service.Decred.Api.Common.Entity;

namespace Lykke.Service.Decred.Api.Services
{    
    public class BalanceService
    {        
        private readonly INosqlRepo<ObservableWalletEntity> _observableWalletRepository;
        private readonly IAddressRepository _addressRepository;
        private readonly IDcrdClient _dcrdClient;
        private readonly IAddressValidationService _addressValidator;

        public BalanceService(
            INosqlRepo<ObservableWalletEntity> observableWalletRepository,
            IAddressRepository addressRepository,
            IDcrdClient dcrdClient,
            IAddressValidationService addressValidator)
        {
            _observableWalletRepository = observableWalletRepository;
            _addressRepository = addressRepository;
            _dcrdClient = dcrdClient;
            _addressValidator = addressValidator;
        }
        
        /// <summary>
        /// Adds the given address to the observation repository.
        /// 
        /// If the address is already in the repository, a BusinessException is thrown.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task SubscribeAsync(string address)
        {
            if (!_addressValidator.IsValid(address))
                throw new BusinessException(ErrorReason.InvalidAddress, "Address is invalid");
            
            await _observableWalletRepository.InsertAsync(new ObservableWalletEntity { Address = address }, false);
        }

        /// <summary>
        /// Removes given address from observation repository.
        /// If the address is not in the repository, a BusinessException is thrown.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="BusinessException"></exception>
        public async Task UnsubscribeAsync(string address)
        {
            if (!_addressValidator.IsValid(address))
                throw new BusinessException(ErrorReason.InvalidAddress, "Address is invalid");

            var entity = new ObservableWalletEntity(){ Address = address };
            await _observableWalletRepository.DeleteAsync(entity);
        }

        /// <summary>
        /// Retrieves balances for all subscribed addresses
        /// </summary>
        /// <param name="take">Max number of balances to retrieve</param>
        /// <param name="continuation">Determines position in the data set to start from</param>
        /// <returns></returns>
        public async Task<PaginationResponse<WalletBalanceContract>> GetBalancesAsync(int take, string continuation)
        {
            var result = await _observableWalletRepository.GetDataWithContinuationTokenAsync(take, continuation);            
            var addresses = result.Entities.Select(e => e.Address).ToArray();
            var blockHeight = await _dcrdClient.GetMaxConfirmedBlockHeight();
            
            var balances = 
               (from balance in await _addressRepository.GetAddressBalancesAsync(addresses, blockHeight)
                where balance.Balance > 0
                select new WalletBalanceContract
                {
                    AssetId = "DCR",
                    Block = balance.BlockHeight,
                    Address = balance.Address,
                    Balance = balance.Balance.ToString()
                }).ToArray();
            
            return new PaginationResponse<WalletBalanceContract> { 
                Items = balances.ToArray(), 
                Continuation = result.ContinuationToken
            };
        }
    }
}
