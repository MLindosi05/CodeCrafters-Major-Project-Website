<%@ Page Title="Change booking" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ManageBooking.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Bookings.ManageBooking" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
<section class="section">
  <h1 class="section-title">Change your booking</h1>
  <asp:Label ID="lblError" runat="server" Visible="false" CssClass="form-msg error" />
  <asp:Panel ID="pnlChange" runat="server" CssClass="form-regal">
    <h2>Booking #<asp:Literal ID="litBookingId" runat="server" /></h2>
    <p><strong>Current stay:</strong> <asp:Literal ID="litStay" runat="server" /> · <asp:Literal ID="litBranch" runat="server" /></p>
    <input type="hidden" id="changeCsrf" value="<%= ChangeCsrfToken %>" />
    <input type="hidden" id="bookingId" value="<%= BookingId %>" />
    <label>Change type
      <select id="changeMode"><option value="Dates">Change dates</option><option value="Upgrade">Upgrade a room</option><option value="Branch">Change branch</option></select>
    </label>
    <div id="dateFields">
      <label>Check-in <input id="changeCheckIn" type="date" value="<%= InitialCheckIn %>" required /></label>
      <label>Check-out <input id="changeCheckOut" type="date" value="<%= InitialCheckOut %>" required /></label>
    </div>
    <div id="roomFields" style="display:none">
      <label>Booking room <asp:DropDownList ID="ddlSourceRoom" runat="server" ClientIDMode="Static" /></label>
      <div id="branchField" style="display:none"><label>New branch <asp:DropDownList ID="ddlBranch" runat="server" ClientIDMode="Static" /></label></div>
      <label id="targetRoomField" style="display:none">Available upgrade room <select id="targetRoom"><option value="">Select a room</option></select></label>
    </div>
    <button type="button" id="quoteButton" class="btn-outline">Check price and availability</button>
    <div id="quoteBox" class="booking-policy-note" style="display:none"></div>
    <div id="changeMessage" class="form-msg" style="display:none"></div>
    <div id="changePayPal" style="margin-top:1rem"></div>
    <button type="button" id="applyButton" class="btn-regal" style="display:none">Confirm change</button>
  </asp:Panel>
  <a class="btn-outline" href="MyBookings.aspx">Back to My Bookings</a>
</section>
<script src="https://www.paypal.com/sdk/js?client-id=<%= PayPalClientId %>&currency=USD"></script>
<script>
    (function () {
        var endpoint = '<%= ResolveUrl("~/PayPal/BookingChange.ashx") %>';
    var csrf = document.getElementById('changeCsrf').value;
    var mode = document.getElementById('changeMode');
    var message = document.getElementById('changeMessage');
    var quoteBox = document.getElementById('quoteBox');
    var quoteButton = document.getElementById('quoteButton');
    var applyButton = document.getElementById('applyButton');
    var paypalBox = document.getElementById('changePayPal');
    var currentQuote = null;

    function requestData() {
        return {
            bookingId: document.getElementById('bookingId').value,
            mode: mode.value,
            checkIn: document.getElementById('changeCheckIn').value,
            checkOut: document.getElementById('changeCheckOut').value,
            sourceRoomId: document.getElementById('ddlSourceRoom').value,
            targetRoomId: document.getElementById('targetRoom').value,
            targetBranchId: document.getElementById('ddlBranch').value
        };
    }

    function showMessage(text, isError) {
        message.textContent = text;
        message.className = isError ? 'form-msg error' : 'form-msg success';
        message.style.display = 'block';
    }

    function clearQuote() {
        currentQuote = null;
        quoteBox.style.display = 'none';
        applyButton.style.display = 'none';
        paypalBox.innerHTML = '';
    }

    function validate(needTarget) {
        var d = requestData();

        if (!d.bookingId) return 'Booking details are missing. Return to My Bookings and try again.';

        if (d.mode === 'Dates') {
            if (!d.checkIn || !d.checkOut) return 'Choose both check-in and check-out dates.';
            if (d.checkOut <= d.checkIn) return 'Check-out must be after check-in.';
        }

        if (d.mode === 'Upgrade') {
            if (!d.sourceRoomId) return 'Choose which booked room you want to upgrade.';
            if (needTarget && !d.targetRoomId) return 'Choose an available upgrade room.';
        }

        if (d.mode === 'Branch' && !d.targetBranchId)
            return 'Choose the branch you want to move the booking to.';

        return '';
    }

    function post(action, data) {
        var controller = new AbortController();
        var timeout = setTimeout(function () { controller.abort(); }, 45000);

        return fetch(endpoint + '?action=' + encodeURIComponent(action), {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(Object.assign({}, data, { csrfToken: csrf })),
            signal: controller.signal
        }).then(function (response) {
            return response.text().then(function (text) {
                var result;
                try {
                    result = JSON.parse(text);
                } catch (_) {
                    throw new Error('The server returned an unreadable response. Refresh the page and try again.');
                }
                if (!response.ok) throw new Error(result.error || 'The request could not be completed.');
                return result;
            });
        }).catch(function (error) {
            if (error.name === 'AbortError')
                throw new Error('The request timed out. Check your connection and try again.');
            throw error;
        }).finally(function () {
            clearTimeout(timeout);
        });
    }

    function confirmChange() {
        var d = requestData();
        var summary =
            'Booking #' + d.bookingId +
            '\nDates: ' + d.checkIn + ' to ' + d.checkOut +
            '\nBranch: ' + (currentQuote ? currentQuote.branch : '') +
            '\nNew total: R' +
            (currentQuote ? Number(currentQuote.newTotal).toFixed(2) : '0.00');

        return RegalDialog.confirm(
            'Are you sure you wish to make this booking change?\n' + summary,
            { title: 'Confirm booking change', okText: 'Continue' }
        );
    }

    function showQuote(q) {
        currentQuote = q;

        var rooms = (q.rooms || []).map(function (room) {
            return 'Room ' + room.roomNumber + ' · ' + room.type +
                ' — R' + Number(room.rate).toFixed(2) + '/night';
        }).join('\n');

        quoteBox.textContent =
            'New branch: ' + q.branch +
            '\nNew total: R' + Number(q.newTotal).toFixed(2) +
            '\nDifference: ' + (q.difference >= 0 ? '+' : '−') +
            'R' + Math.abs(Number(q.difference)).toFixed(2) +
            (rooms ? '\n' + rooms : '');

        quoteBox.style.whiteSpace = 'pre-line';
        quoteBox.style.display = 'block';
        message.style.display = 'none';

        if (Number(q.difference) > 0) {
            applyButton.style.display = 'none';

            if (!window.paypal || !window.paypal.Buttons) {
                showMessage('PayPal did not load. Refresh the page and try again.', true);
                return;
            }

            paypalBox.innerHTML = '';

            paypal.Buttons({
                onClick: function (data, actions) {
                    var error = validate(true);
                    if (error) {
                        showMessage(error, true);
                        return actions.reject();
                    }

                    return confirmChange().then(function (confirmed) {
                        if (!confirmed) {
                            showMessage('Payment cancelled. No booking change was made.', false);
                            return actions.reject();
                        }
                        return actions.resolve();
                    });
                },

                createOrder: function () {
                    var d = requestData();
                    d.expectedNewTotal = currentQuote.newTotal;

                    return post('createOrder', d).then(function (result) {
                        if (!result.id) throw new Error('PayPal did not return an order number.');
                        return result.id;
                    });
                },

                onApprove: function (data) {
                    showMessage('Payment approved by PayPal. Updating your booking…', false);

                    return post('capture', { orderID: data.orderID }).then(function (result) {
                        showMessage(result.message || 'Your booking was updated.', false);
                        return RegalDialog.alert(result.message || 'Your booking was updated.', {
                            title: 'Booking updated'
                        }).then(function () {
                            window.location.href = 'MyBookings.aspx';
                        });
                    }).catch(function (error) {
                        showMessage(error.message, true);
                    });
                },

                onCancel: function () {
                    showMessage('PayPal payment was cancelled. No booking change was made.', false);
                },

                onError: function (error) {
                    showMessage(
                        error && error.message
                            ? 'PayPal could not complete the payment: ' + error.message
                            : 'PayPal could not complete the payment. Please try again.',
                        true
                    );
                }
            }).render('#changePayPal').catch(function (error) {
                showMessage('PayPal could not load its payment button. Please refresh and try again.', true);
            });
        } else {
            paypalBox.innerHTML = '';
            applyButton.style.display = 'inline-block';
        }
    }

    function loadOptions() {
        clearQuote();

        var isUpgrade = mode.value === 'Upgrade';
        document.getElementById('roomFields').style.display = mode.value === 'Dates' ? 'none' : 'block';
        document.getElementById('dateFields').style.display = mode.value === 'Dates' ? 'block' : 'none';
        document.getElementById('branchField').style.display = mode.value === 'Branch' ? 'block' : 'none';
        document.getElementById('targetRoomField').style.display = isUpgrade ? 'block' : 'none';

        if (!isUpgrade) return;

        var error = validate(false);
        if (error) {
            showMessage(error, true);
            return;
        }

        post('options', requestData()).then(function (result) {
            var target = document.getElementById('targetRoom');
            target.innerHTML = '<option value="">Select a room</option>';

            (result.options || []).forEach(function (option) {
                var item = document.createElement('option');
                item.value = option.roomId;
                item.textContent = 'Room ' + option.roomNumber + ' — R' +
                    Number(option.rate).toFixed(2) + '/night';
                target.appendChild(item);
            });

            if (!result.options || !result.options.length)
                showMessage('No available upgrade rooms were found for this booking.', false);
        }).catch(function (error) {
            showMessage(error.message, true);
        });
    }

    mode.addEventListener('change', loadOptions);
    document.getElementById('ddlSourceRoom').addEventListener('change', loadOptions);
    document.getElementById('ddlBranch').addEventListener('change', clearQuote);
    document.getElementById('targetRoom').addEventListener('change', clearQuote);
    document.getElementById('changeCheckIn').addEventListener('change', clearQuote);
    document.getElementById('changeCheckOut').addEventListener('change', clearQuote);

    quoteButton.addEventListener('click', function () {
        var error = validate(true);
        if (error) {
            showMessage(error, true);
            return;
        }

        quoteButton.disabled = true;
        quoteButton.textContent = 'Checking availability…';
        clearQuote();

        post('quote', requestData()).then(showQuote).catch(function (error) {
            showMessage(error.message, true);
        }).finally(function () {
            quoteButton.disabled = false;
            quoteButton.textContent = 'Check price and availability';
        });
    });

    applyButton.addEventListener('click', function () {
        if (!currentQuote) {
            showMessage('Check price and availability before confirming the change.', true);
            return;
        }

        var error = validate(true);
        if (error) {
            showMessage(error, true);
            return;
        }

        confirmChange().then(function (confirmed) {
            if (!confirmed) return;

            applyButton.disabled = true;
            applyButton.textContent = 'Updating booking…';

            var data = requestData();
            data.expectedNewTotal = currentQuote.newTotal;

            post('apply', data).then(function (result) {
                return RegalDialog.alert(result.message || 'Your booking was updated.', {
                    title: 'Booking updated'
                }).then(function () {
                    window.location.href = 'MyBookings.aspx';
                });
            }).catch(function (error) {
                showMessage(error.message, true);
                applyButton.disabled = false;
                applyButton.textContent = 'Confirm change';
            });
        });
    });

    loadOptions();
}());
</script>
</asp:Content>
