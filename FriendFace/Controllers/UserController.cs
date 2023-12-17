using FriendFace.Services.DatabaseService;
using FriendFace.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FriendFace.Controllers
{
    public class UserController : Controller
    {
        private readonly UserQueryService _userQueryService;
        private readonly ILogger<UserController> _logger;
        private readonly UserManager<User> _userManager;


        public UserController(ILogger<UserController> logger, UserQueryService userQueryService, UserManager<User> userManager)
        {
            _userQueryService = userQueryService;
            _logger = logger;
            _userManager = userManager;

        }

        [HttpGet("User/Profile/{userId}")]
        public IActionResult Profile(int userId)
        {
            // Retrieve the profile user based on the userId.
            _logger.LogInformation("Profile action called with userId: {UserId}", userId);

            var profileUser = _userQueryService.GetSimpleUserById(userId); 
            if (profileUser == null)
            {
                _logger.LogWarning("No user found with userId: {UserId}", userId);
                return NotFound();
            }

            var loggedInUser = _userQueryService.GetLoggedInUser();
            bool isLoggedIn = loggedInUser != null;
            bool isCurrentUser = loggedInUser != null && loggedInUser.Id == profileUser.Id;
            bool isFollowing = loggedInUser != null &&
                    loggedInUser.Following.Any(f => f.FollowingId == profileUser.Id);

            _logger.LogInformation("Profile found. Current user: {IsCurrentUser}", isCurrentUser);

            // Create the ViewModel for the view.
            var viewModel = new UserProfileViewModel
            {
                user = profileUser,
                isCurrentUser = isCurrentUser,
                isFollowing = isFollowing,
                isLoggedIn = isLoggedIn,

                // Populate other properties of the ViewModel as needed.
            };

            // Pass the ViewModel to the view.
            return View("ViewProfile", viewModel);
        }


        [HttpGet("User/Profile/EditProfile")]
        public IActionResult EditProfile()
        {
            // Create an empty UserProfileViewModel or populate it with initial data
            var model = new UserProfileViewModel();

            // Optionally, you can pre-populate the model with user data if needed.

            return View("_EditProfile", model);
        }


        [HttpPost("User/Profile/SaveEditProfile")]
        public async Task<IActionResult> SaveEditProfile([FromForm]UserProfileViewModel model)
        {

            var fieldsToIgnore = new List<string> {
                "ChangePasswordViewModel", "user.Likes", "user.Posts", "user.Comments",
                "user.LastName", "user.FirstName", "user.Followers", "user.Following"
            };

            foreach (var field in fieldsToIgnore)
            {
                ModelState.Remove(field);
            }

            _logger.LogInformation("SaveEditProfile Called");
            var user = await _userManager.GetUserAsync(User); // Retrieve the current user
            if (user == null)
            {
                _logger.LogWarning("User is null");
                // Handle the case where the user is not found
                return NotFound(); // or another appropriate response
            }

            // Update Email if it's provided and different from the current email
            if (!string.IsNullOrWhiteSpace(model.user.Email) && model.user.Email != user.Email)
            {
                user.Email = model.user.Email;
            }

            // Update other fields similarly...
            // if (!string.IsNullOrWhiteSpace(model.user.FirstName) && model.user.FirstName != user.FirstName)
            // {
            //     user.FirstName = model.user.FirstName;
            // }

            // Handle password change if provided
            if (!string.IsNullOrWhiteSpace(model.ChangePasswordViewModel?.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.ChangePasswordViewModel.CurrentPassword))
                {
                    ModelState.AddModelError("ChangePasswordViewModel.CurrentPassword", "Current password is required.");
                    return View("_EditProfile", model);
                }

                var checkPassword = await _userManager.CheckPasswordAsync(user, model.ChangePasswordViewModel.CurrentPassword);
                if (!checkPassword)
                {
                    ModelState.AddModelError("ChangePasswordViewModel.CurrentPassword", "Current password is not correct.");
                    return View("_EditProfile", model);
                }

                if (model.ChangePasswordViewModel.NewPassword != model.ChangePasswordViewModel.ConfirmNewPassword)
                {
                    ModelState.AddModelError("ChangePasswordViewModel.ConfirmNewPassword", "The new password and confirmation password do not match.");
                    return View("_EditProfile", model);
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.ChangePasswordViewModel.CurrentPassword, model.ChangePasswordViewModel.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("_EditProfile", model);
                }
            }


            // Save changes to the user
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                // Handle errors
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                if (!ModelState.IsValid)
{
    foreach (var state in ModelState)
    {
        foreach (var error in state.Value.Errors)
        {
            _logger.LogWarning("Error in {0}: {1}", state.Key, error.ErrorMessage);
        }
    }
}

                if (!ModelState.IsValid || !updateResult.Succeeded)
                {
                    _logger.LogWarning("Modelstate is invalid");
                    // If ModelState is not valid, or user update failed, return the errors in JSON format
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(b => b.ErrorMessage));
                    return Json(new { success = false, errors = errors });
                }

                // If everything is fine for AJAX request, return a success message
                return Json(new { success = true, message = "Profile updated successfully!" });
            }


            // Redirect or return a view upon successful update
            return RedirectToAction("Profile", "User", new {userId = _userQueryService.GetLoggedInUser().Id }); // or another appropriate view
        }

        [HttpGet] // Change to HttpGet for the integration with your JavaScript
        public async Task<IActionResult> FollowUser(int userIdToFollow)
        {
            var loggedInUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(loggedInUserId))
            {
                return Unauthorized();
            }

            var result = await _userQueryService.FollowUser(int.Parse(loggedInUserId), userIdToFollow);
            if (!result)
            {
                return BadRequest();
            }

            return RedirectToAction("Profile", new { userId = userIdToFollow });
        }

        [HttpGet] // Change to HttpGet for the integration with your JavaScript
        public async Task<IActionResult> UnfollowUser(int userIdToUnfollow)
        {
            var loggedInUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(loggedInUserId))
            {
                return Unauthorized();
            }

            var result = await _userQueryService.UnfollowUser(int.Parse(loggedInUserId), userIdToUnfollow);
            if (!result)
            {
                return BadRequest();
            }

            return RedirectToAction("Profile", new { userId = userIdToUnfollow });
        }
    }

}
