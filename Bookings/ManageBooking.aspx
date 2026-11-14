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
(function(){
  var endpoint='<%= ResolveUrl("~/PayPal/BookingChange.ashx") %>',csrf=document.getElementById('changeCsrf').value,mode=document.getElementById('changeMode'),
      message=document.getElementById('changeMessage'),quoteBox=document.getElementById('quoteBox'),apply=document.getElementById('applyButton'),paypalBox=document.getElementById('changePayPal'),currentQuote=null;
  function post(action,data){return fetch(endpoint+'?action='+action,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json'},body:JSON.stringify(Object.assign(data,{csrfToken:csrf}))}).then(function(r){return r.json().then(function(j){if(!r.ok)throw Error(j.error||'Request failed');return j;});});}
  function req(){return {bookingId:document.getElementById('bookingId').value,mode:mode.value,checkIn:document.getElementById('changeCheckIn').value,checkOut:document.getElementById('changeCheckOut').value,sourceRoomId:document.getElementById('ddlSourceRoom').value,targetRoomId:document.getElementById('targetRoom').value,targetBranchId:document.getElementById('ddlBranch').value};}
  function showError(e){message.textContent=e.message;message.className='form-msg error';message.style.display='block';}
  function confirmChange(){return RegalDialog.confirm('Are you sure you wish to make this booking change?\nBooking #'+req().bookingId+'\nDates: '+req().checkIn+' to '+req().checkOut+'\nNew branch: '+(currentQuote?currentQuote.branch:'')+'\nNew total: R'+(currentQuote?Number(currentQuote.newTotal).toFixed(2):'0.00'),{title:'Confirm booking change',okText:'Confirm change'});}
  function loadOptions(){currentQuote=null;apply.style.display='none';paypalBox.innerHTML='';quoteBox.style.display='none';var roomFields=document.getElementById('roomFields'),branchField=document.getElementById('branchField'),targetField=document.getElementById('targetRoomField');
    roomFields.style.display=mode.value==='Dates'?'none':'block';document.getElementById('dateFields').style.display=mode.value==='Dates'?'block':'none';branchField.style.display=mode.value==='Branch'?'block':'none';targetField.style.display=mode.value==='Upgrade'?'block':'none';
    if(mode.value!=='Upgrade')return;
    post('options',req()).then(function(result){var target=document.getElementById('targetRoom');target.innerHTML='<option value="">Select a room</option>';result.options.forEach(function(o){var opt=document.createElement('option');opt.value=o.roomId;opt.textContent=(mode.value==='Branch'?o.type+' · ':'')+'Room '+o.roomNumber+' — R'+Number(o.rate).toFixed(2)+'/night';target.appendChild(opt);});}).catch(showError);
  }
  mode.addEventListener('change',loadOptions);document.getElementById('ddlSourceRoom').addEventListener('change',loadOptions);document.getElementById('ddlBranch').addEventListener('change',loadOptions);document.getElementById('targetRoom').addEventListener('change',function(){currentQuote=null;apply.style.display='none';quoteBox.style.display='none';paypalBox.innerHTML='';});document.getElementById('changeCheckIn').addEventListener('change',function(){currentQuote=null;apply.style.display='none';quoteBox.style.display='none';paypalBox.innerHTML='';});document.getElementById('changeCheckOut').addEventListener('change',function(){currentQuote=null;apply.style.display='none';quoteBox.style.display='none';paypalBox.innerHTML='';});
  document.getElementById('quoteButton').addEventListener('click',function(){post('quote',req()).then(function(q){currentQuote=q;var lines=q.rooms.map(function(r){return 'Room '+r.roomNumber+' · '+r.type+' — R'+Number(r.rate).toFixed(2)+'/night';}).join('\n');quoteBox.textContent='New branch: '+q.branch+'\nNew total: R'+Number(q.newTotal).toFixed(2)+'\nDifference: '+(q.difference>=0?'+':'−')+'R'+Math.abs(Number(q.difference)).toFixed(2)+'\n'+lines;quoteBox.style.whiteSpace='pre-line';quoteBox.style.display='block';message.style.display='none';if(q.difference>0){apply.style.display='none';paypalBox.innerHTML='';paypal.Buttons({createOrder:function(){return confirmChange().then(function(yes){if(!yes)throw Error('Payment cancelled.');var data=req();data.expectedNewTotal=currentQuote.newTotal;return post('createOrder',data).then(function(r){return r.id;});});},onApprove:function(d){return post('capture',{orderID:d.orderID}).then(function(r){message.textContent=r.message;message.className='form-msg success';message.style.display='block';RegalDialog.alert(r.message,{title:'Booking updated'}).then(function(){location.href='MyBookings.aspx';});}).catch(showError);}}).render('#changePayPal');}else{paypalBox.innerHTML='';apply.style.display='inline-block';}}).catch(showError);});
  apply.addEventListener('click',function(){confirmChange().then(function(yes){if(!yes)return;apply.disabled=true;var data=req();data.expectedNewTotal=currentQuote.newTotal;post('apply',data).then(function(r){message.textContent=r.message;message.className='form-msg success';message.style.display='block';RegalDialog.alert(r.message,{title:'Booking updated'}).then(function(){location.href='MyBookings.aspx';});}).catch(function(e){showError(e);apply.disabled=false;});});});
  loadOptions();
})();
</script>
</asp:Content>
