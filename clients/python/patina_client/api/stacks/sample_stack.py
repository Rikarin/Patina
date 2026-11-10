from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_find_request import StackFindRequest
from ...models.stack_sample import StackSample
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: StackFindRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/stacks/{id}/sample".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | StackSample | None:
    if response.status_code == 200:
        response_200 = StackSample.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | StackSample]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackFindRequest,

) -> Response[ProblemDetails | StackSample]:
    """ What every channel holds at a place on the map, as a material picker takes it up: one texel of each,
    read back from the composite at full size, as numbers.

    Args:
        id (str):
        body (StackFindRequest): Asks which layer is on top at a place on a stack's map, as a
            click on the mesh or the map finds it.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackSample]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackFindRequest,

) -> ProblemDetails | StackSample | None:
    """ What every channel holds at a place on the map, as a material picker takes it up: one texel of each,
    read back from the composite at full size, as numbers.

    Args:
        id (str):
        body (StackFindRequest): Asks which layer is on top at a place on a stack's map, as a
            click on the mesh or the map finds it.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackSample
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackFindRequest,

) -> Response[ProblemDetails | StackSample]:
    """ What every channel holds at a place on the map, as a material picker takes it up: one texel of each,
    read back from the composite at full size, as numbers.

    Args:
        id (str):
        body (StackFindRequest): Asks which layer is on top at a place on a stack's map, as a
            click on the mesh or the map finds it.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackSample]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackFindRequest,

) -> ProblemDetails | StackSample | None:
    """ What every channel holds at a place on the map, as a material picker takes it up: one texel of each,
    read back from the composite at full size, as numbers.

    Args:
        id (str):
        body (StackFindRequest): Asks which layer is on top at a place on a stack's map, as a
            click on the mesh or the map finds it.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackSample
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
